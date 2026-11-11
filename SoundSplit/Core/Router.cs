using System;
using System.Collections.Generic;
using System.Linq;

namespace SoundSplit.Core
{
    /// <summary>
    /// Keeps Windows in sync with the saved rules: every running process of a routed app gets
    /// told which device to use. New processes (e.g. you open Spotify later) are picked up on
    /// the next tick, and routing is undone when a rule is removed or SoundSplit is turned off.
    /// </summary>
    internal sealed class Router
    {
        private readonly IEndpointPolicy _policy;
        private readonly Func<string, List<int>> _pidsForExe;
        private readonly Func<int, bool> _isAlive;
        // (pid, isInput) -> device id we last told Windows about
        private readonly Dictionary<(int Pid, bool Input), string> _applied = new();

        public Router(IEndpointPolicy policy, Func<string, List<int>> pidsForExe, Func<int, bool> isAlive)
        {
            _policy = policy;
            _pidsForExe = pidsForExe;
            _isAlive = isAlive;
        }

        public bool IsAvailable => _policy.IsAvailable;

        public int RoutedProcessCount => _applied.Keys.Select(k => k.Pid).Distinct().Count();

        public void Sync(IReadOnlyDictionary<string, RouteRule> rules, bool enabled,
                         ISet<string> activeOutputs, ISet<string> activeInputs)
        {
            if (!_policy.IsAvailable) return;
            var wanted = new Dictionary<(int, bool), string>();

            if (enabled)
            {
                foreach (var (exeKey, rule) in rules)
                {
                    if (!rule.HasRouting) continue;
                    var outId = rule.OutputDeviceId;
                    var inId = rule.InputDeviceId;
                    // Device unplugged? Skip; Windows falls back to the default device on its own,
                    // and we re-apply as soon as it comes back.
                    var useOut = !string.IsNullOrEmpty(outId) && activeOutputs.Contains(outId);
                    var useIn = !string.IsNullOrEmpty(inId) && activeInputs.Contains(inId);
                    if (!useOut && !useIn) continue;

                    foreach (var pid in _pidsForExe(exeKey))
                    {
                        if (useOut) wanted[(pid, false)] = outId!;
                        if (useIn) wanted[(pid, true)] = inId!;
                    }
                }
            }

            foreach (var (key, deviceId) in wanted)
            {
                if (_applied.TryGetValue(key, out var current) && current == deviceId) continue;
                var ok = key.Item2 ? _policy.SetAppInput(key.Item1, deviceId) : _policy.SetAppOutput(key.Item1, deviceId);
                if (ok) _applied[key] = deviceId;
            }

            // Anything we routed before but no longer should: hand it back to "Default".
            foreach (var key in _applied.Keys.ToList())
            {
                if (wanted.ContainsKey(key)) continue;
                if (_isAlive(key.Pid))
                {
                    if (key.Input) _policy.SetAppInput(key.Pid, null);
                    else _policy.SetAppOutput(key.Pid, null);
                }
                _applied.Remove(key);
            }
        }

        /// <summary>Immediately send one app back to the default device (both output and mic).</summary>
        public void ResetApp(string exeKey)
        {
            foreach (var pid in _pidsForExe(exeKey))
            {
                _policy.SetAppOutput(pid, null);
                _policy.SetAppInput(pid, null);
                _applied.Remove((pid, false));
                _applied.Remove((pid, true));
            }
        }

        public bool ResetEverything()
        {
            _applied.Clear();
            return _policy.ClearAll();
        }
    }
}
