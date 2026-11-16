using SoundSplit.Core;
using Xunit;

namespace SoundSplit.Tests
{
    public class UpdateVersionTests
    {
        // Fault: comparing versions as text ("1.10.0" < "1.9.0") would hide real updates.
        [Fact]
        public void Two_digit_parts_compare_as_numbers() => Assert.True(UpdateVersion.IsNewer("v1.10.0", "1.9.0"));

        // Fault: offering the version you already have -> endless "update available" popup.
        [Fact]
        public void Same_version_is_not_an_update() => Assert.False(UpdateVersion.IsNewer("v1.3.0", "1.3.0"));

        // Fault: offering an older release downgrades the app.
        [Fact]
        public void Older_version_is_not_an_update() => Assert.False(UpdateVersion.IsNewer("v1.2.0", "1.3.0"));

        [Fact]
        public void Missing_parts_count_as_zero()
        {
            Assert.False(UpdateVersion.IsNewer("1.3", "1.3.0"));
            Assert.True(UpdateVersion.IsNewer("1.3.1", "1.3"));
        }

        // Fault: a malformed tag crashing the check or being treated as newer.
        [Theory]
        [InlineData("latest")]
        [InlineData("v1.x")]
        [InlineData("")]
        public void Garbage_is_never_newer(string tag) => Assert.False(UpdateVersion.IsNewer(tag, "1.0.0"));
    }
}
