using System;

namespace SoundSplit.Core
{
    /// <summary>
    /// Converts between Core Audio device ids ("{0.0.0.00000000}.{guid}") and the device-interface
    /// paths the per-app routing API expects ("\\?\SWD#MMDEVAPI#{id}#{interface-class}").
    /// </summary>
    internal static class EndpointIds
    {
        public const string MmDevApiToken = @"\\?\SWD#MMDEVAPI#";
        public const string RenderInterface = "#{e6327cad-dcec-4949-ae8a-991e976a79d2}";
        public const string CaptureInterface = "#{2eef81be-33fa-4800-9670-1cd474972c3f}";

        public static string ToEndpointPath(string mmDeviceId, bool capture) =>
            MmDevApiToken + mmDeviceId + (capture ? CaptureInterface : RenderInterface);

        public static string? FromEndpointPath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var s = path;
            if (s.StartsWith(MmDevApiToken, StringComparison.OrdinalIgnoreCase)) s = s.Substring(MmDevApiToken.Length);
            if (s.EndsWith(RenderInterface, StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - RenderInterface.Length);
            else if (s.EndsWith(CaptureInterface, StringComparison.OrdinalIgnoreCase)) s = s.Substring(0, s.Length - CaptureInterface.Length);
            return s.Length == 0 ? null : s;
        }
    }
}
