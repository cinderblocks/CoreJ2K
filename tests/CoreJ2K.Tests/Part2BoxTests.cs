// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using CoreJ2K;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for JPEG 2000 Part 2 boxes: JPR (Intellectual Property Rights) and Label boxes.
    /// </summary>
    public class Part2BoxTests
    {
        #region JPR Box Tests

        [Fact]
        public void JprBox_DefaultConstructor_HasNoData()
        {
            var jpr = new JprBox();

            Assert.Null(jpr.Text);
            Assert.Null(jpr.RawData);
            Assert.False(jpr.IsBinary);
        }

        [Fact]
        public void JprBox_SetText_StoresCorrectly()
        {
            var jpr = new JprBox
            {
                Text = "Copyright 2025 Test Company. All rights reserved."
            };

            Assert.Equal("Copyright 2025 Test Company. All rights reserved.", jpr.Text);
            Assert.Null(jpr.RawData);
            Assert.False(jpr.IsBinary);
            Assert.Equal(jpr.Text, jpr.GetText());
        }

        [Fact]
        public void JprBox_SetRawData_IsBinary()
        {
            var data = Encoding.UTF8.GetBytes("Copyright 2025");
            var jpr = new JprBox
            {
                RawData = data
            };

            Assert.True(jpr.IsBinary);
            Assert.Equal("Copyright 2025", jpr.GetText());
        }

        [Fact]
        public void JprBox_ToString_FormatsCorrectly()
        {
            var jpr = new JprBox
            {
                Text = "Copyright 2025 Test Company. All rights reserved."
            };

            var str = jpr.ToString();

            Assert.Contains("JPR Box", str);
            Assert.Contains("Copyright 2025", str);
        }

        [Fact]
        public void JprBox_ToString_HandlesLongText()
        {
            var longText = new string('A', 100);
            var jpr = new JprBox { Text = longText };

            var str = jpr.ToString();

            Assert.Contains("...", str); // Should be truncated
            Assert.True(str.Length < longText.Length + 20);
        }

        [Fact]
        public void JprBox_GetText_PrefersTextOverBinary()
        {
            var jpr = new JprBox
            {
                Text = "Original Text",
                RawData = Encoding.UTF8.GetBytes("Binary Text")
            };

            // Text takes precedence when both are set
            Assert.Equal("Original Text", jpr.GetText());
        }

        #endregion

        #region Label Box Tests

        [Fact]
        public void LabelBox_DefaultConstructor_HasNoData()
        {
            var label = new LabelBox();

            Assert.Null(label.Label);
            Assert.Null(label.RawData);
            Assert.False(label.IsBinary);
        }

        [Fact]
        public void LabelBox_SetLabel_StoresCorrectly()
        {
            var label = new LabelBox
            {
                Label = "Image Title: Sunset over Mountains"
            };

            Assert.Equal("Image Title: Sunset over Mountains", label.Label);
            Assert.Null(label.RawData);
            Assert.False(label.IsBinary);
            Assert.Equal(label.Label, label.GetLabel());
        }

        [Fact]
        public void LabelBox_SetRawData_IsBinary()
        {
            var data = Encoding.UTF8.GetBytes("Image Label");
            var label = new LabelBox
            {
                RawData = data
            };

            Assert.True(label.IsBinary);
            Assert.Equal("Image Label", label.GetLabel());
        }

        [Fact]
        public void LabelBox_ToString_FormatsCorrectly()
        {
            var label = new LabelBox
            {
                Label = "Test Image Label"
            };

            var str = label.ToString();

            Assert.Contains("Label Box", str);
            Assert.Contains("Test Image Label", str);
        }

        [Fact]
        public void LabelBox_ToString_HandlesLongLabel()
        {
            var longLabel = new string('B', 100);
            var label = new LabelBox { Label = longLabel };

            var str = label.ToString();

            Assert.Contains("...", str); // Should be truncated
            Assert.True(str.Length < longLabel.Length + 20);
        }

        [Fact]
        public void LabelBox_GetLabel_PrefersLabelOverBinary()
        {
            var label = new LabelBox
            {
                Label = "Original Label",
                RawData = Encoding.UTF8.GetBytes("Binary Label")
            };

            // Label takes precedence when both are set
            Assert.Equal("Original Label", label.GetLabel());
        }

        #endregion

        #region Metadata Integration Tests

        [Fact]
        public void J2KMetadata_AddIntellectualPropertyRights_AddsToCollection()
        {
            var metadata = new J2KMetadata();

            metadata.AddIntellectualPropertyRights("Copyright 2025 Test");

            Assert.Single(metadata.IntellectualPropertyRights);
            Assert.Equal("Copyright 2025 Test", metadata.IntellectualPropertyRights[0].Text);
        }

        [Fact]
        public void J2KMetadata_AddLabel_AddsToCollection()
        {
            var metadata = new J2KMetadata();

            metadata.AddLabel("Test Label");

            Assert.Single(metadata.Labels);
            Assert.Equal("Test Label", metadata.Labels[0].Label);
        }

        [Fact]
        public void J2KMetadata_MultipleJprBoxes_AllStored()
        {
            var metadata = new J2KMetadata();

            metadata.AddIntellectualPropertyRights("Copyright 2025");
            metadata.AddIntellectualPropertyRights("Patent Pending");
            metadata.AddIntellectualPropertyRights("Trademark Notice");

            Assert.Equal(3, metadata.IntellectualPropertyRights.Count);
            Assert.Equal("Copyright 2025", metadata.IntellectualPropertyRights[0].Text);
            Assert.Equal("Patent Pending", metadata.IntellectualPropertyRights[1].Text);
            Assert.Equal("Trademark Notice", metadata.IntellectualPropertyRights[2].Text);
        }

        [Fact]
        public void J2KMetadata_MultipleLabels_AllStored()
        {
            var metadata = new J2KMetadata();

            metadata.AddLabel("Title");
            metadata.AddLabel("Description");
            metadata.AddLabel("Keywords");

            Assert.Equal(3, metadata.Labels.Count);
            Assert.Equal("Title", metadata.Labels[0].Label);
            Assert.Equal("Description", metadata.Labels[1].Label);
            Assert.Equal("Keywords", metadata.Labels[2].Label);
        }

        [Fact]
        public void J2KMetadata_DefaultConstructor_EmptyCollections()
        {
            var metadata = new J2KMetadata();

            Assert.Empty(metadata.IntellectualPropertyRights);
            Assert.Empty(metadata.Labels);
        }

        [Fact]
        public void JprBox_UnicodeText_HandledCorrectly()
        {
            var unicodeText = "?? 2025 © Test™ Société ???";
            var jpr = new JprBox { Text = unicodeText };

            Assert.Equal(unicodeText, jpr.GetText());
        }

        [Fact]
        public void LabelBox_EmojiText_HandledCorrectly()
        {
            var emojiText = "?? Sunset Photo ??";
            var label = new LabelBox { Label = emojiText };

            Assert.Equal(emojiText, label.GetLabel());
        }

        #endregion
    }
}
