// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Globalization;
using CoreJ2K.j2k.util;

namespace CoreJ2K.j2k.encoder
{
    /// <summary>The four kinds of subband of a wavelet decomposition.</summary>
    public enum WaveletSubband
    {
        /// <summary>Low-pass in both directions. Only the lowest resolution level (0) has one.</summary>
        LL = 0,

        /// <summary>High-pass horizontally, low-pass vertically.</summary>
        HL = 1,

        /// <summary>Low-pass horizontally, high-pass vertically.</summary>
        LH = 2,

        /// <summary>High-pass in both directions.</summary>
        HH = 3
    }

    /// <summary>One rule of a <see cref="DistortionWeights"/> table.</summary>
    public sealed class DistortionWeight
    {
        /// <summary>Gets the factor applied to the distortion of the code-blocks the rule matches.</summary>
        public double Weight { get; }

        /// <summary>Gets the component the rule applies to, or -1 for every component.</summary>
        public int Component { get; }

        /// <summary>Gets the resolution level the rule applies to (0 is the lowest), or -1 for every level.</summary>
        public int Resolution { get; }

        /// <summary>Gets the kind of subband the rule applies to, or null for every kind.</summary>
        public WaveletSubband? Subband { get; }

        internal DistortionWeight(double weight, int component, int resolution, WaveletSubband? subband)
        {
            Weight = weight;
            Component = component;
            Resolution = resolution;
            Subband = subband;
        }

        internal bool Matches(int component, int resolution, WaveletSubband subband) =>
            (Component < 0 || Component == component)
            && (Resolution < 0 || Resolution == resolution)
            && (!Subband.HasValue || Subband.Value == subband);

        /// <inheritdoc/>
        public override string ToString() => FormattableString.Invariant(
            $"{Weight:R}{(Component >= 0 ? ":c" + Component : "")}{(Resolution >= 0 ? ":r" + Resolution : "")}{(Subband.HasValue ? ":" + Subband.Value : "")}");
    }

    /// <summary>
    /// Weights on the distortion of code-blocks, by component, resolution level and subband. The rate allocator keeps the bytes that
    /// buy the most reduction in weighted distortion, so a weight above 1 makes the encoder spend more on the matching
    /// code-blocks at the expense of the rest, and a weight below 1 the opposite. Use it to favour luma over chroma, or the fine
    /// detail that a viewer sees over the bands that matter less.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A code-block's weight is the product of every rule that matches it, and 1 if none does. Only the allocation changes: the
    /// codestream is an ordinary one, and nothing in it records the weights. A lossless encode keeps every pass whatever the
    /// weights; with several quality layers they still change how the passes are divided between the layers.
    /// </para>
    /// <para>
    /// Resolution level 0 holds the LL band of the deepest decomposition; levels 1 and up hold HL, LH and HH bands, the highest
    /// level being the finest detail.
    /// </para>
    /// </remarks>
    public sealed class DistortionWeights
    {
        /// <summary>The smallest weight a rule may have.</summary>
        public const double MinimumWeight = 1.0 / 16;

        /// <summary>The largest weight a rule may have.</summary>
        public const double MaximumWeight = 16;

        private readonly List<DistortionWeight> _rules = new List<DistortionWeight>();

        /// <summary>Gets the rules, in the order they were added.</summary>
        public IReadOnlyList<DistortionWeight> Rules => _rules.AsReadOnly();

        /// <summary>
        /// Adds a rule: code-blocks that match every condition given are weighted by <paramref name="weight"/>.
        /// </summary>
        /// <param name="weight">The factor, from <see cref="MinimumWeight"/> to <see cref="MaximumWeight"/>.</param>
        /// <param name="component">The component (0-based), or -1 for all.</param>
        /// <param name="resolution">The resolution level (0 is the lowest), or -1 for all.</param>
        /// <param name="subband">The kind of subband, or null for all.</param>
        /// <returns>This instance for method chaining.</returns>
        public DistortionWeights Add(double weight, int component = -1, int resolution = -1, WaveletSubband? subband = null)
        {
            _rules.Add(new DistortionWeight(weight, component, resolution, subband));
            return this;
        }

        /// <summary>Weights every code-block of one component, such as the luma component after the colour transform.</summary>
        public DistortionWeights ForComponent(int component, double weight) => Add(weight, component: component);

        /// <summary>Weights every code-block of one resolution level.</summary>
        public DistortionWeights ForResolution(int resolution, double weight) => Add(weight, resolution: resolution);

        /// <summary>Weights every code-block of one kind of subband.</summary>
        public DistortionWeights ForSubband(WaveletSubband subband, double weight) => Add(weight, subband: subband);

        /// <summary>
        /// Returns the factor for the code-blocks of the given component, resolution level and subband: the product of the matching rules.
        /// </summary>
        public double GetWeight(int component, int resolution, WaveletSubband subband)
        {
            var weight = 1.0;
            foreach (var rule in _rules)
            {
                if (rule.Matches(component, resolution, subband)) weight *= rule.Weight;
            }
            return weight;
        }

        /// <summary>Returns the problems with the rules, if any.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var rule in _rules)
            {
                if (double.IsNaN(rule.Weight) || rule.Weight < MinimumWeight || rule.Weight > MaximumWeight)
                    errors.Add(string.Format(CultureInfo.InvariantCulture,
                        "Distortion weight {0} is outside {1} to {2}", rule.Weight, MinimumWeight, MaximumWeight));
                if (rule.Component < -1)
                    errors.Add($"Distortion weight component must be -1 (all) or a 0-based index (got {rule.Component})");
                if (rule.Resolution < -1)
                    errors.Add($"Distortion weight resolution must be -1 (all) or a 0-based level (got {rule.Resolution})");
                if (rule.Subband.HasValue && !Enum.IsDefined(typeof(WaveletSubband), rule.Subband.Value))
                    errors.Add($"Unknown subband {rule.Subband.Value}");
                if (rule.Subband == WaveletSubband.LL && rule.Resolution > 0)
                    errors.Add($"The LL band exists only at resolution level 0 (got level {rule.Resolution})");
                if (rule.Subband.HasValue && rule.Subband != WaveletSubband.LL && rule.Resolution == 0)
                    errors.Add($"Resolution level 0 holds only the LL band (got {rule.Subband})");
            }
            return errors;
        }

        /// <summary>
        /// Checks the rules against the image being coded, so that one that names a component or resolution level the image does
        /// not have fails instead of silently matching nothing.
        /// </summary>
        /// <param name="source">The quantizer, first tile.</param>
        internal void CheckAgainst(CoreJ2K.j2k.quantization.quantizer.Quantizer source)
        {
            var components = source.NumComps;
            var highest = new int[components];
            source.SetTile(0, 0);
            for (var c = 0; c < components; c++) highest[c] = source.GetAnSubbandTree(0, c).resLvl;

            foreach (var rule in _rules)
            {
                if (rule.Component >= components)
                    throw new ArgumentException($"Distortion weight '{rule}' names component {rule.Component}, but the image has {components}.");
                if (rule.Resolution < 0) continue;

                var best = 0;
                for (var c = 0; c < components; c++)
                {
                    if (rule.Component < 0 || rule.Component == c) best = Math.Max(best, highest[c]);
                }
                if (rule.Resolution > best)
                    throw new ArgumentException(
                        $"Distortion weight '{rule}' names resolution level {rule.Resolution}, but the highest is {best} (decomposition levels).");
            }
        }

        /// <summary>Gets whether any rule changes a weight.</summary>
        internal bool IsNeutral
        {
            get
            {
                foreach (var rule in _rules)
                {
                    if (rule.Weight != 1) return false;
                }
                return true;
            }
        }

        /// <summary>The name of the encoder option that carries the weights.</summary>
        internal const string OptionName = "Dweights";

        /// <summary>
        /// Writes the rules into the encoder parameter list as the <c>Dweights</c> option: words of the form
        /// <c>weight[:c&lt;component&gt;][:r&lt;resolution&gt;][:LL|HL|LH|HH]</c>, separated by spaces.
        /// </summary>
        internal void ApplyTo(ParameterList pl)
        {
            if (_rules.Count == 0) return;
            var words = new string[_rules.Count];
            for (var i = 0; i < words.Length; i++) words[i] = _rules[i].ToString();
            pl[OptionName] = string.Join(" ", words);
        }

        /// <summary>Reads the <c>Dweights</c> option, or returns null if it is not set.</summary>
        /// <exception cref="ArgumentException">The option is malformed or has a weight out of range.</exception>
        internal static DistortionWeights FromParameterList(ParameterList pl)
        {
            var text = pl.GetParameter(OptionName);
            if (string.IsNullOrWhiteSpace(text)) return null;

            var weights = new DistortionWeights();
            foreach (var word in text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = word.Split(':');
                if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var weight))
                    throw new ArgumentException($"Bad weight in '-{OptionName}': {word}");

                int component = -1, resolution = -1;
                WaveletSubband? subband = null;
                for (var i = 1; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part.Length > 1 && part[0] == 'c' && int.TryParse(part.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var c))
                        component = c;
                    else if (part.Length > 1 && part[0] == 'r' && int.TryParse(part.Substring(1), NumberStyles.None, CultureInfo.InvariantCulture, out var r))
                        resolution = r;
                    else if (Enum.TryParse(part, out WaveletSubband band) && Enum.IsDefined(typeof(WaveletSubband), band) && part == band.ToString())
                        subband = band;
                    else
                        throw new ArgumentException($"Bad condition '{part}' in '-{OptionName}': {word}");
                }
                weights.Add(weight, component, resolution, subband);
            }

            var errors = weights.Validate();
            if (errors.Count > 0)
                throw new ArgumentException($"Invalid '-{OptionName}': {string.Join("; ", errors)}");
            return weights;
        }
    }
}
