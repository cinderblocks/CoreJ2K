// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using Xunit;
using CoreJ2K.Color.ICC;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for ICC profile support in JPEG2000 files.
    /// </summary>
    public class ICCProfileTests
    {
        /// <summary>
        /// Creates a minimal valid ICC profile header for testing.
        /// </summary>
        private byte[] CreateMinimalIccProfile()
        {
            var profile = new byte[128]; // Minimum ICC profile size

            // Profile size (offset 0-3, big-endian)
            profile[0] = 0x00;
            profile[1] = 0x00;
            profile[2] = 0x00;
            profile[3] = 0x80; // 128 bytes

            // Preferred CMM type (offset 4-7)
            profile[4] = profile[5] = profile[6] = profile[7] = 0;

            // Profile version (offset 8-9)
            profile[8] = 0x02; // Version 2
            profile[9] = 0x10; // .1

            // Profile/Device class (offset 12-15) - 'mntr' for display
            profile[12] = (byte)'m';
            profile[13] = (byte)'n';
            profile[14] = (byte)'t';
            profile[15] = (byte)'r';

            // Color space (offset 16-19) - 'RGB '
            profile[16] = (byte)'R';
            profile[17] = (byte)'G';
            profile[18] = (byte)'B';
            profile[19] = (byte)' ';

            // Profile connection space (offset 20-23) - 'XYZ '
            profile[20] = (byte)'X';
            profile[21] = (byte)'Y';
            profile[22] = (byte)'Z';
            profile[23] = (byte)' ';

            return profile;
        }

        [Fact]
        public void ICCProfileData_ValidProfile_ParsesCorrectly()
        {
            var profileBytes = CreateMinimalIccProfile();
            var iccProfile = new ICCProfileData(profileBytes);

            Assert.True(iccProfile.IsValid);
            Assert.Equal(128, iccProfile.ProfileSize);
            Assert.Equal(new Version(2, 1), iccProfile.ProfileVersion);
            Assert.Equal("mntr", iccProfile.ProfileClass);
            Assert.Equal("RGB ", iccProfile.ColorSpaceType);
        }

        [Fact]
        public void ICCProfileData_TooSmall_IsInvalid()
        {
            var profileBytes = new byte[64]; // Too small
            var iccProfile = new ICCProfileData(profileBytes);

            Assert.False(iccProfile.IsValid);
        }

        [Fact]
        public void ICCProfileData_Null_IsInvalid()
        {
            var iccProfile = new ICCProfileData(null);

            Assert.False(iccProfile.IsValid);
        }

        [Fact]
        public void ICCProfileData_WrongSize_IsInvalid()
        {
            var profileBytes = CreateMinimalIccProfile();
            // Modify size to be incorrect
            profileBytes[3] = 0xFF; // Claim 255 bytes but only have 128

            var iccProfile = new ICCProfileData(profileBytes);

            Assert.False(iccProfile.IsValid);
        }

        [Fact]
        public void ICCProfileData_ToString_ReturnsFormattedString()
        {
            var profileBytes = CreateMinimalIccProfile();
            var iccProfile = new ICCProfileData(profileBytes);

            var str = iccProfile.ToString();

            Assert.Contains("ICC Profile", str);
            Assert.Contains("v2.1", str);
            Assert.Contains("mntr", str);
            Assert.Contains("RGB", str);
            Assert.Contains("128 bytes", str);
        }

        [Fact]
        public void J2KMetadata_SetIccProfile_StoresProfile()
        {
            var metadata = new J2KMetadata();
            var profileBytes = CreateMinimalIccProfile();

            metadata.SetIccProfile(profileBytes);

            Assert.NotNull(metadata.IccProfile);
            Assert.True(metadata.IccProfile.IsValid);
            Assert.Equal(128, metadata.IccProfile.ProfileSize);
        }

        [Fact]
        public void J2KMetadata_SetIccProfile_CreatesDefensiveCopy()
        {
            var metadata = new J2KMetadata();
            var profileBytes = CreateMinimalIccProfile();

            metadata.SetIccProfile(profileBytes);

            // Modify original array
            profileBytes[0] = 0xFF;

            // Stored profile should be unchanged
            Assert.NotEqual(0xFF, metadata.IccProfile.ProfileBytes[0]);
        }

        [Fact]
        public void ICCProfileData_ColorSpaceConstants_AreCorrect()
        {
            Assert.Equal("XYZ ", ICCProfileData.ColorSpaces.XYZ);
            Assert.Equal("Lab ", ICCProfileData.ColorSpaces.Lab);
            Assert.Equal("RGB ", ICCProfileData.ColorSpaces.RGB);
            Assert.Equal("GRAY", ICCProfileData.ColorSpaces.Gray);
            Assert.Equal("CMYK", ICCProfileData.ColorSpaces.CMYK);
        }

        [Fact]
        public void ICCProfileData_ProfileClassConstants_AreCorrect()
        {
            Assert.Equal("scnr", ICCProfileData.ProfileClasses.Input);
            Assert.Equal("mntr", ICCProfileData.ProfileClasses.Display);
            Assert.Equal("prtr", ICCProfileData.ProfileClasses.Output);
            Assert.Equal("link", ICCProfileData.ProfileClasses.Link);
            Assert.Equal("spac", ICCProfileData.ProfileClasses.ColorSpace);
        }

        [Fact]
        public void ICCProfileData_GrayscaleProfile_ParsesCorrectly()
        {
            var profile = CreateMinimalIccProfile();
            
            // Change to grayscale color space
            profile[16] = (byte)'G';
            profile[17] = (byte)'R';
            profile[18] = (byte)'A';
            profile[19] = (byte)'Y';

            var iccProfile = new ICCProfileData(profile);

            Assert.True(iccProfile.IsValid);
            Assert.Equal("GRAY", iccProfile.ColorSpaceType);
        }

        [Fact]
        public void ICCProfileData_CMYKProfile_ParsesCorrectly()
        {
            var profile = CreateMinimalIccProfile();
            
            // Change to CMYK color space
            profile[16] = (byte)'C';
            profile[17] = (byte)'M';
            profile[18] = (byte)'Y';
            profile[19] = (byte)'K';
            
            // Change to output device class
            profile[12] = (byte)'p';
            profile[13] = (byte)'r';
            profile[14] = (byte)'t';
            profile[15] = (byte)'r';

            var iccProfile = new ICCProfileData(profile);

            Assert.True(iccProfile.IsValid);
            Assert.Equal("CMYK", iccProfile.ColorSpaceType);
            Assert.Equal("prtr", iccProfile.ProfileClass);
        }
    }
}
