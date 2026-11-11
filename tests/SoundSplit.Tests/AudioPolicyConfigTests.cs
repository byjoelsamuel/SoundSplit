using System;
using System.Diagnostics;
using System.Linq;
using SoundSplit.Core;
using Xunit;

namespace SoundSplit.Tests
{
    public class AudioPolicyConfigTests
    {
        // Fault (shipped in v1.0.0): only one interface id was tried, so Windows answered
        // E_NOINTERFACE ("Specified cast is not valid") on builds that use the other one.
        [Fact]
        public void Both_known_interface_ids_are_tried()
        {
            var ids = AudioPolicyConfig.CandidateIids.Select(g => g.ToString()).ToList();
            Assert.Contains("2a59116d-6c4f-45e0-a74f-707e3fef9258", ids);
            Assert.Contains("ab3d4648-e242-459f-b02f-541c70306324", ids);
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        // Real Windows check (runs in CI on windows-latest). If activation fails it must not be
        // because of a wrong interface id (E_NOINTERFACE = 0x80004002).
        [SkippableFact]
        public void Windows_accepts_one_of_the_interface_ids()
        {
            Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");
            var policy = new AudioPolicyConfig();
            Skip.If(!policy.IsAvailable && (policy.Error ?? "").Contains("80040154"),
                    "AudioPolicyConfig class isn't registered on this Windows edition");
            Assert.True(policy.IsAvailable, policy.Error);
        }

        // Fault: wrong number of placeholder methods -> calls land on the wrong vtable slot and
        // either crash or return garbage. Reading and clearing our own route must both work.
        [SkippableFact]
        public void Vtable_layout_reaches_the_right_methods()
        {
            Skip.IfNot(OperatingSystem.IsWindows(), "Windows only");
            var policy = new AudioPolicyConfig();
            Skip.IfNot(policy.IsAvailable, policy.Error ?? "unavailable");
            // Routing calls go through the Windows Audio service, which is off on some CI servers.
            Skip.IfNot(AudioServiceRunning(), "Windows Audio service isn't running on this machine");
            var pid = Environment.ProcessId;
            Assert.True(policy.SetAppOutput(pid, null), $"clearing our own route failed: HRESULT 0x{policy.LastHResult:X8}");
            Assert.Null(policy.GetAppOutput(pid));
        }

        private static bool AudioServiceRunning()
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo("sc.exe", "query Audiosrv")
                    { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }
}
