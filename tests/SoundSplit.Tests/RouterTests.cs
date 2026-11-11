using System.Collections.Generic;
using System.Linq;
using SoundSplit.Core;
using Xunit;

namespace SoundSplit.Tests
{
    public class RouterTests
    {
        private sealed class FakePolicy : IEndpointPolicy
        {
            public bool IsAvailable { get; set; } = true;
            public int FailNext { get; set; }
            public List<(int Pid, bool Input, string? Device)> Calls { get; } = new();
            public bool SetAppOutput(int pid, string? id) => Record(pid, false, id);
            public bool SetAppInput(int pid, string? id) => Record(pid, true, id);
            public bool ClearAll() { Calls.Add((-1, false, "clear")); return true; }
            private bool Record(int pid, bool input, string? id)
            {
                Calls.Add((pid, input, id));
                if (FailNext > 0) { FailNext--; return false; }
                return true;
            }
        }

        private readonly FakePolicy _policy = new();
        private readonly Dictionary<string, List<int>> _procs = new() { ["spotify.exe"] = new() { 10, 11 } };
        private readonly HashSet<int> _alive = new() { 10, 11, 12 };
        private readonly Dictionary<string, RouteRule> _rules = new();
        private readonly HashSet<string> _outs = new() { "dac", "bt" };
        private readonly HashSet<string> _ins = new() { "mic" };
        private readonly Router _router;

        public RouterTests()
        {
            _router = new Router(_policy, k => _procs.TryGetValue(k, out var l) ? l.ToList() : new List<int>(), p => _alive.Contains(p));
            _rules["spotify.exe"] = new RouteRule { OutputDeviceId = "dac" };
        }

        private void Sync(bool enabled = true) => _router.Sync(_rules, enabled, _outs, _ins);

        // Fault: only the first process is routed (Chrome/Discord/Spotify run several).
        [Fact]
        public void Routes_every_process_of_the_app()
        {
            Sync();
            Assert.Equal(new[] { (10, false, (string?)"dac"), (11, false, (string?)"dac") }, _policy.Calls.OrderBy(c => c.Pid));
        }

        // Fault: Windows is told again every tick, which can restart the app's audio stream.
        [Fact]
        public void Does_not_repeat_calls_when_nothing_changed()
        {
            Sync(); _policy.Calls.Clear();
            Sync(); Sync();
            Assert.Empty(_policy.Calls);
        }

        // Fault: switching SoundSplit off leaves apps stuck on the other device.
        [Fact]
        public void Turning_off_sends_routed_apps_back_to_default()
        {
            Sync(); _policy.Calls.Clear();
            Sync(enabled: false);
            Assert.Equal(new[] { (10, false, (string?)null), (11, false, (string?)null) }, _policy.Calls.OrderBy(c => c.Pid));
        }

        [Fact]
        public void Removing_a_rule_sends_the_app_back_to_default()
        {
            Sync(); _policy.Calls.Clear();
            _rules.Clear();
            Sync();
            Assert.All(_policy.Calls, c => Assert.Null(c.Device));
            Assert.Equal(2, _policy.Calls.Count);
        }

        [Fact]
        public void Changing_the_device_reapplies_it()
        {
            Sync(); _policy.Calls.Clear();
            _rules["spotify.exe"].OutputDeviceId = "bt";
            Sync();
            Assert.Equal(new[] { (10, false, (string?)"bt"), (11, false, (string?)"bt") }, _policy.Calls.OrderBy(c => c.Pid));
        }

        // Fault: routing to an unplugged device, or never re-applying after it's plugged back in.
        [Fact]
        public void Unplugged_device_is_skipped_and_reapplied_when_it_returns()
        {
            _outs.Remove("dac");
            Sync();
            Assert.DoesNotContain(_policy.Calls, c => c.Device == "dac");
            _outs.Add("dac");
            Sync();
            Assert.Equal(2, _policy.Calls.Count(c => c.Device == "dac"));
        }

        [Fact]
        public void A_newly_started_process_gets_routed()
        {
            Sync(); _policy.Calls.Clear();
            _procs["spotify.exe"].Add(12);
            Sync();
            Assert.Equal(new[] { (12, false, (string?)"dac") }, _policy.Calls);
        }

        // Fault: resetting a dead pid (which Windows may have reused for another app).
        [Fact]
        public void Exited_process_is_not_touched()
        {
            Sync(); _policy.Calls.Clear();
            _procs["spotify.exe"].Remove(10);
            _alive.Remove(10);
            Sync();
            Assert.DoesNotContain(_policy.Calls, c => c.Pid == 10);
        }

        // Fault: mic and output flows swapped.
        [Fact]
        public void Mic_rule_only_changes_the_input()
        {
            _rules["spotify.exe"] = new RouteRule { InputDeviceId = "mic" };
            Sync();
            Assert.All(_policy.Calls, c => Assert.True(c.Input));
            Assert.Equal(2, _policy.Calls.Count);
        }

        // Fault: marking a route as applied even though Windows refused it, so it's never retried.
        [Fact]
        public void A_refused_call_is_retried_on_the_next_tick()
        {
            _policy.FailNext = 2;
            Sync(); _policy.Calls.Clear();
            Sync();
            Assert.Equal(2, _policy.Calls.Count(c => c.Device == "dac"));
        }

        [Fact]
        public void Does_nothing_when_the_api_is_unavailable()
        {
            _policy.IsAvailable = false;
            Sync();
            Assert.Empty(_policy.Calls);
        }
    }
}
