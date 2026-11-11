using SoundSplit.Core;
using Xunit;

namespace SoundSplit.Tests
{
    public class EndpointIdsTests
    {
        private const string Id = "{0.0.0.00000000}.{1b2c3d4e-0000-4000-8000-123456789abc}";

        // Fault: render and capture interface classes swapped -> outputs would be routed as mics.
        [Fact]
        public void Output_path_uses_the_render_interface_class_exactly()
        {
            Assert.Equal(@"\\?\SWD#MMDEVAPI#" + Id + "#{e6327cad-dcec-4949-ae8a-991e976a79d2}",
                         EndpointIds.ToEndpointPath(Id, capture: false));
        }

        [Fact]
        public void Mic_path_uses_the_capture_interface_class_exactly()
        {
            Assert.Equal(@"\\?\SWD#MMDEVAPI#" + Id + "#{2eef81be-33fa-4800-9670-1cd474972c3f}",
                         EndpointIds.ToEndpointPath(Id, capture: true));
        }

        // Fault: unpacking strips the wrong part, so the app shows the wrong current device.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Unpacking_returns_the_original_device_id(bool capture)
        {
            Assert.Equal(Id, EndpointIds.FromEndpointPath(EndpointIds.ToEndpointPath(Id, capture)));
        }

        // Windows hands these back in whatever case it likes.
        [Fact]
        public void Unpacking_ignores_case()
        {
            var path = EndpointIds.ToEndpointPath(Id, false).ToLowerInvariant();
            Assert.Equal(Id.ToLowerInvariant(), EndpointIds.FromEndpointPath(path));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Empty_path_means_no_route(string? path) => Assert.Null(EndpointIds.FromEndpointPath(path));
    }
}
