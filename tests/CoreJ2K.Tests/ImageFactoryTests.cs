using System;
using Xunit;
using CoreJ2K.Util;

namespace CoreJ2K.Tests
{
    public class ImageFactoryTests
    {
        [Fact]
        public void ImageFactory_FindPlatformCreators_ShouldReturnList()
        {
            // ImageFactory static ctor populates creators via J2kSetup.FindCodecs
            // Ensure it doesn't throw and that New/ToPortableImageSource behave for null
            var img = ImageFactory.New<object>(1, 1, 1, new byte[1]);
            // No creators for object expected => null
            Assert.Null(img);

            Assert.Null(ImageFactory.ToPortableImageSource(null));
        }

        [Fact]
        public void J2kSetup_GetSinglePlatformInstance_ReturnsNullOnMissing()
        {
            var inst = J2kSetup.GetSinglePlatformInstance<IDisposable>();
            Assert.Null(inst);
        }
    }
}
