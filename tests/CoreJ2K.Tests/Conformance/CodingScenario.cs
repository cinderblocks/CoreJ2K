// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CoreJ2K.j2k.roi;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// One randomly drawn way of coding an image: its size, depth, tiling, wavelet, code-blocks, precincts, progression, layers,
    /// entropy-coder modes and markers. A scenario is a pure function of its seed, so a failure is reproduced by the seed alone, and
    /// it renders itself both as CoreJ2K options and as <c>opj_compress</c> arguments.
    /// </summary>
    internal sealed class CodingScenario
    {
        public int Seed { get; internal set; }

        // image
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public int Components { get; internal set; }
        public int Bits { get; internal set; }
        public bool Signed { get; internal set; }
        public int OffsetX { get; internal set; }
        public int OffsetY { get; internal set; }

        // tiling
        public int TileWidth { get; internal set; }
        public int TileHeight { get; internal set; }
        public int TileOffsetX { get; internal set; }
        public int TileOffsetY { get; internal set; }

        // transform and quantization
        public bool Lossless { get; internal set; }
        public int Levels { get; internal set; }
        public float StepSize { get; internal set; }
        public bool DerivedQuantization { get; internal set; }
        public int GuardBits { get; internal set; }
        public bool ColourTransform { get; internal set; }

        // code-blocks, precincts, progression, layers
        public int BlockWidth { get; internal set; }
        public int BlockHeight { get; internal set; }
        public int[] Precincts { get; internal set; } = Array.Empty<int>(); // width, height pairs from the highest resolution
        public string Progression { get; internal set; } = "layer";
        public int Layers { get; internal set; } // 0: the encoder's default layers

        // entropy coder modes
        public bool Bypass { get; internal set; }
        public bool Terminate { get; internal set; }
        public bool ResetContexts { get; internal set; }
        public bool VerticallyCausal { get; internal set; }
        public bool SegmentationSymbols { get; internal set; }

        // markers and packaging
        public bool Sop { get; internal set; }
        public bool Eph { get; internal set; }
        public bool PackedHeadersInMain { get; internal set; }
        public bool PackedHeadersInTile { get; internal set; }
        public bool Plt { get; internal set; }
        public bool Tlm { get; internal set; }
        public int PacketsPerTilePart { get; internal set; }
        public bool FileFormat { get; internal set; }

        // region of interest (Maxshift on a rectangle of component -1)
        public bool Roi { get; internal set; }
        public int RoiX { get; internal set; }
        public int RoiY { get; internal set; }
        public int RoiWidth { get; internal set; }
        public int RoiHeight { get; internal set; }

        public int Threads { get; internal set; }

        public bool Tiled => TileWidth > 0;

        /// <summary>The number of tiles the image is cut into.</summary>
        public int TileCount => !Tiled ? 1
            : (int)Math.Ceiling((OffsetX + Width - TileOffsetX) / (double)TileWidth) * (int)Math.Ceiling((OffsetY + Height - TileOffsetY) / (double)TileHeight);

        /// <summary>The smallest width or height, in samples, of any tile of the image.</summary>
        public int SmallestTileDimension
        {
            get
            {
                if (!Tiled) return Math.Min(Width, Height);
                return Math.Min(SmallestExtent(OffsetX, Width, TileOffsetX, TileWidth), SmallestExtent(OffsetY, Height, TileOffsetY, TileHeight));
            }
        }

        private static int SmallestExtent(int origin, int size, int tileOrigin, int tileSize)
        {
            var end = origin + size;
            var smallest = int.MaxValue;
            for (var start = tileOrigin; start < end; start += tileSize)
            {
                var lo = Math.Max(start, origin);
                var hi = Math.Min(start + tileSize, end);
                if (hi > lo) smallest = Math.Min(smallest, hi - lo);
            }
            return smallest;
        }

        /// <summary>A copy with some settings changed, for narrowing down which setting a failure depends on.</summary>
        public CodingScenario With(Action<CodingScenario> change)
        {
            var copy = (CodingScenario)MemberwiseClone();
            change(copy);
            return copy;
        }

        public static CodingScenario FromSeed(int seed)
        {
            var rnd = new Random(seed);
            var s = new CodingScenario { Seed = seed };

            // ---- image
            var sizeClass = rnd.Next(10);
            if (sizeClass < 2) { s.Width = rnd.Next(1, 9); s.Height = rnd.Next(1, 9); }
            else if (sizeClass < 3) { s.Width = rnd.Next(1, 4); s.Height = rnd.Next(20, 120); }
            else if (sizeClass < 4) { s.Width = rnd.Next(20, 120); s.Height = rnd.Next(1, 4); }
            else { s.Width = rnd.Next(9, 140); s.Height = rnd.Next(9, 140); }

            s.Components = new[] { 1, 1, 1, 2, 3, 3, 3, 4, 5 }[rnd.Next(9)];
            s.Bits = rnd.Next(10) < 5 ? 8 : rnd.Next(1, 17);
            s.Signed = rnd.Next(7) == 0;
            if (rnd.Next(4) == 0) { s.OffsetX = rnd.Next(0, 40); s.OffsetY = rnd.Next(0, 40); }

            // ---- tiling
            if (rnd.Next(3) == 0)
            {
                s.TileWidth = rnd.Next(8, Math.Max(9, s.Width + 20));
                s.TileHeight = rnd.Next(8, Math.Max(9, s.Height + 20));
                if (rnd.Next(3) == 0)
                {
                    // the tile grid may start before the image, but the image must lie in its first tile
                    s.TileOffsetX = Math.Max(0, s.OffsetX - rnd.Next(0, Math.Min(s.OffsetX, s.TileWidth - 1) + 1));
                    s.TileOffsetY = Math.Max(0, s.OffsetY - rnd.Next(0, Math.Min(s.OffsetY, s.TileHeight - 1) + 1));
                    if (s.OffsetX - s.TileOffsetX >= s.TileWidth) s.TileOffsetX = s.OffsetX;
                    if (s.OffsetY - s.TileOffsetY >= s.TileHeight) s.TileOffsetY = s.OffsetY;
                }
                else
                {
                    s.TileOffsetX = s.OffsetX >= s.TileWidth ? s.OffsetX : 0;
                    s.TileOffsetY = s.OffsetY >= s.TileHeight ? s.OffsetY : 0;
                }
            }

            // ---- transform and quantization
            s.Lossless = rnd.Next(5) < 3;
            s.Levels = rnd.Next(8) == 0 ? 0 : rnd.Next(1, 7);
            s.StepSize = new[] { 1f / 512, 1f / 256, 1f / 128, 1f / 64 }[rnd.Next(4)];
            s.DerivedQuantization = !s.Lossless && rnd.Next(5) == 0;
            s.GuardBits = rnd.Next(4) == 0 ? rnd.Next(2, 7) : 2; // one guard bit overflows for some data, in OpenJPEG as well
            s.ColourTransform = s.Components >= 3 && rnd.Next(4) != 0;

            // ---- code-blocks, precincts
            var wExp = rnd.Next(2, 9);
            var hExp = rnd.Next(2, Math.Min(9, 12 - wExp) + 1);
            s.BlockWidth = 1 << wExp;
            s.BlockHeight = 1 << hExp;
            if (rnd.Next(10) < 4) { s.BlockWidth = 64; s.BlockHeight = 64; }
            if (rnd.Next(3) == 0)
            {
                var count = rnd.Next(1, s.Levels + 2);
                s.Precincts = Enumerable.Range(0, count * 2).Select(_ => 1 << rnd.Next(4, 10)).ToArray();
            }

            // ---- progression and layers
            s.Progression = new[] { "layer", "res", "res-pos", "pos-comp", "comp-pos" }[rnd.Next(5)];
            s.Layers = rnd.Next(3) == 0 ? rnd.Next(1, 6) : 0;

            // ---- entropy coder modes
            s.Bypass = rnd.Next(5) == 0;
            s.Terminate = rnd.Next(5) == 0;
            s.ResetContexts = rnd.Next(5) == 0;
            s.VerticallyCausal = rnd.Next(5) == 0;
            s.SegmentationSymbols = rnd.Next(5) == 0;

            // ---- markers and packaging
            s.Sop = rnd.Next(4) == 0;
            s.Eph = rnd.Next(4) == 0;
            var packed = rnd.Next(8);
            s.PackedHeadersInMain = packed == 0;
            s.PackedHeadersInTile = packed == 1;
            s.Plt = rnd.Next(6) == 0;
            s.Tlm = rnd.Next(6) == 0;
            s.PacketsPerTilePart = rnd.Next(6) == 0 ? rnd.Next(1, 12) : 0;
            s.FileFormat = rnd.Next(3) == 0;

            // ---- region of interest
            if (rnd.Next(8) == 0 && s.Width >= 4 && s.Height >= 4)
            {
                s.Roi = true;
                s.RoiWidth = rnd.Next(2, s.Width + 1);
                s.RoiHeight = rnd.Next(2, s.Height + 1);
                s.RoiX = rnd.Next(0, s.Width - s.RoiWidth + 1);
                s.RoiY = rnd.Next(0, s.Height - s.RoiHeight + 1);
            }

            s.Threads = rnd.Next(3) == 0 ? 1 : 0;
            return s;
        }

        public SampleImage MakeImage() => SampleImage.Generate(new Random(Seed * 7919 + 13), Width, Height, Components, Bits, Signed);

        /// <summary>Whether the file the scenario produces has bits it cannot reproduce exactly (lossy coding).</summary>
        public bool ExpectExact => Lossless;

        public string Describe()
        {
            var parts = new List<string>
            {
                $"seed={Seed}",
                $"{Width}x{Height}x{Components} {Bits}bit{(Signed ? " signed" : "")}",
                Lossless ? "lossless" : $"lossy(step 1/{(int)Math.Round(1 / StepSize)}{(DerivedQuantization ? ", derived" : "")})",
                $"levels={Levels}",
                $"cblk={BlockWidth}x{BlockHeight}",
                $"order={Progression}",
            };
            if (OffsetX != 0 || OffsetY != 0) parts.Add($"origin=({OffsetX},{OffsetY})");
            if (Tiled) parts.Add($"tiles={TileWidth}x{TileHeight}@({TileOffsetX},{TileOffsetY})");
            if (Precincts.Length > 0) parts.Add("precincts=" + string.Join(",", Precincts.Where((_, i) => i % 2 == 0).Zip(Precincts.Where((_, i) => i % 2 == 1), (w, h) => $"{w}x{h}")));
            if (Layers > 0) parts.Add($"layers={Layers}");
            if (GuardBits != 2) parts.Add($"guard={GuardBits}");
            if (ColourTransform) parts.Add("mct");
            var modes = new[] { Bypass ? "bypass" : null, Terminate ? "termall" : null, ResetContexts ? "reset" : null, VerticallyCausal ? "causal" : null, SegmentationSymbols ? "segsym" : null }
                .Where(m => m != null).ToList();
            if (modes.Count > 0) parts.Add("modes=" + string.Join("+", modes));
            var marks = new[] { Sop ? "SOP" : null, Eph ? "EPH" : null, PackedHeadersInMain ? "PPM" : null, PackedHeadersInTile ? "PPT" : null, Plt ? "PLT" : null, Tlm ? "TLM" : null }
                .Where(m => m != null).ToList();
            if (marks.Count > 0) parts.Add(string.Join("+", marks));
            if (PacketsPerTilePart > 0) parts.Add($"tileparts/{PacketsPerTilePart}pkts");
            if (FileFormat) parts.Add("jp2");
            if (Roi) parts.Add($"roi=({RoiX},{RoiY},{RoiWidth},{RoiHeight})");
            return string.Join(" ", parts);
        }

        // ---- CoreJ2K ------------------------------------------------------------------------------------------------

        public ParameterList ToParameterList()
        {
            var pl = new ParameterList(J2kImage.GetDefaultEncoderParameterList());
            pl["file_format"] = FileFormat ? "on" : "off";
            pl["verbose"] = "off";
            if (Threads > 0) pl["threads"] = Threads.ToString(CultureInfo.InvariantCulture);
            pl["Wlev"] = Levels.ToString(CultureInfo.InvariantCulture);
            if (Lossless)
            {
                pl["lossless"] = "on";
            }
            else
            {
                pl["Ffilters"] = "w9x7";
                pl["Qtype"] = DerivedQuantization ? "derived" : "expounded";
                pl["Qstep"] = StepSize.ToString("R", CultureInfo.InvariantCulture);
            }
            pl["Qguard_bits"] = GuardBits.ToString(CultureInfo.InvariantCulture);
            pl["Mct"] = ColourTransform ? "on" : "off";
            pl["Cblksiz"] = $"{BlockWidth} {BlockHeight}";
            if (Precincts.Length > 0) pl["Cpp"] = string.Join(" ", Precincts);
            pl["Aptype"] = Progression;
            if (Layers == 1) pl["Alayers"] = "sl";
            else if (Layers > 1) pl["Alayers"] = string.Join(" ", Enumerable.Range(1, Layers - 1).Select(i => (0.25 * i).ToString(CultureInfo.InvariantCulture))) + " 8.0";
            pl["Cbypass"] = Bypass ? "on" : "off";
            pl["Cterminate"] = Terminate ? "on" : "off";
            pl["CresetMQ"] = ResetContexts ? "on" : "off";
            pl["Ccausal"] = VerticallyCausal ? "on" : "off";
            pl["Cseg_symbol"] = SegmentationSymbols ? "on" : "off";
            pl["Psop"] = Sop ? "on" : "off";
            pl["Peph"] = Eph ? "on" : "off";
            pl["pph_main"] = PackedHeadersInMain ? "on" : "off";
            pl["pph_tile"] = PackedHeadersInTile ? "on" : "off";
            pl["Hplt"] = Plt ? "on" : "off";
            pl["Htlm"] = Tlm ? "on" : "off";
            if (PacketsPerTilePart > 0) pl["tile_parts"] = PacketsPerTilePart.ToString(CultureInfo.InvariantCulture);
            pl["tiles"] = Tiled ? $"{TileWidth} {TileHeight}" : "0 0";
            pl["ref"] = $"{OffsetX} {OffsetY}";
            pl["tref"] = $"{TileOffsetX} {TileOffsetY}";
            if (Roi)
            {
                pl["Rroi"] = "M 0";
                pl["Rstart_level"] = Math.Max(0, Levels - 1).ToString(CultureInfo.InvariantCulture);
                pl["Ralign"] = "off";
                pl.RoiMasks.Add(ROIMask.FromPredicate(Width, Height, (x, y) => x >= RoiX && x < RoiX + RoiWidth && y >= RoiY && y < RoiY + RoiHeight));
            }
            return pl;
        }

        // ---- OpenJPEG -----------------------------------------------------------------------------------------------

        /// <summary>The <c>opj_compress</c> arguments for this scenario, for the part of it OpenJPEG can express.</summary>
        public List<string> ToOpjArguments(string rawPath, string outPath)
        {
            var args = new List<string>
            {
                "-i", rawPath,
                "-F", $"{Width},{Height},{Components},{Bits},{(Signed ? "s" : "u")}",
                "-o", outPath,
                "-n", (Levels + 1).ToString(CultureInfo.InvariantCulture),
                "-b", $"{BlockWidth},{BlockHeight}",
                "-p", Progression switch { "layer" => "LRCP", "res" => "RLCP", "res-pos" => "RPCL", "pos-comp" => "PCRL", _ => "CPRL" },
                "-mct", ColourTransform ? "1" : "0",
                "-GuardBits", GuardBits.ToString(CultureInfo.InvariantCulture),
            };
            if (!Lossless) args.Add("-I");
            if (Precincts.Length > 0)
                args.AddRange(new[] { "-c", string.Join(",", Enumerable.Range(0, Precincts.Length / 2).Select(i => $"[{Precincts[2 * i]},{Precincts[2 * i + 1]}]")) });
            if (Tiled) args.AddRange(new[] { "-t", $"{TileWidth},{TileHeight}" });
            if (OffsetX != 0 || OffsetY != 0) args.AddRange(new[] { "-d", $"{OffsetX},{OffsetY}" });
            if (TileOffsetX != 0 || TileOffsetY != 0) args.AddRange(new[] { "-T", $"{TileOffsetX},{TileOffsetY}" });
            var mode = (Bypass ? 1 : 0) | (ResetContexts ? 2 : 0) | (Terminate ? 4 : 0) | (VerticallyCausal ? 8 : 0) | (SegmentationSymbols ? 32 : 0);
            if (mode != 0) args.AddRange(new[] { "-M", mode.ToString(CultureInfo.InvariantCulture) });
            if (Sop) args.Add("-SOP");
            if (Eph) args.Add("-EPH");
            if (Plt) args.Add("-PLT");
            if (Tlm) args.Add("-TLM");
            if (Layers > 1)
            {
                // decreasing compression ratios, ending lossless (or at the step size's own quality when lossy)
                args.AddRange(new[] { "-r", string.Join(",", Enumerable.Range(0, Layers).Select(i => i == Layers - 1 ? "1" : (4 << (Layers - 1 - i)).ToString(CultureInfo.InvariantCulture))) });
            }
            return args;
        }
    }
}
