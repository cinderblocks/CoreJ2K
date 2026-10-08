// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreJ2K.j2k.util;

namespace CoreJ2K.Configuration
{
    /// <summary>
    /// Configuration for tile partitioning.
    /// </summary>
    public class TileConfiguration
    {
        /// <summary>Gets or sets the tile width in pixels.</summary>
        public int Width { get; set; } = 0; // 0 means no tiling
        
        /// <summary>Gets or sets the tile height in pixels.</summary>
        public int Height { get; set; } = 0;
        
        /// <summary>Gets or sets the image reference point X coordinate.</summary>
        public int ReferenceX { get; set; } = 0;
        
        /// <summary>Gets or sets the image reference point Y coordinate.</summary>
        public int ReferenceY { get; set; } = 0;
        
        /// <summary>Gets or sets the tiling reference point X coordinate.</summary>
        public int TilingReferenceX { get; set; } = 0;
        
        /// <summary>Gets or sets the tiling reference point Y coordinate.</summary>
        public int TilingReferenceY { get; set; } = 0;
        
        /// <summary>Gets or sets the maximum packets per tile-part.</summary>
        public int PacketsPerTilePart { get; set; } = 0; // 0 means all in first tile-part
        
        /// <summary>
        /// Sets the tile size. Use (0, 0) to disable tiling.
        /// </summary>
        public TileConfiguration SetSize(int width, int height)
        {
            Width = width;
            Height = height;
            return this;
        }
        
        /// <summary>
        /// Sets the image reference point (origin on canvas).
        /// </summary>
        public TileConfiguration WithImageReference(int x, int y)
        {
            ReferenceX = x;
            ReferenceY = y;
            return this;
        }
        
        /// <summary>
        /// Sets the tiling reference point.
        /// </summary>
        public TileConfiguration WithTilingReference(int x, int y)
        {
            TilingReferenceX = x;
            TilingReferenceY = y;
            return this;
        }
        
        /// <summary>
        /// Sets the maximum number of packets per tile-part.
        /// </summary>
        public TileConfiguration WithPacketsPerTilePart(int packets)
        {
            PacketsPerTilePart = packets;
            return this;
        }
        
        internal void ApplyTo(ParameterList pl)
        {
            pl["tiles"] = $"{Width} {Height}";
            pl["ref"] = $"{ReferenceX} {ReferenceY}";
            pl["tref"] = $"{TilingReferenceX} {TilingReferenceY}";
            pl["tile_parts"] = PacketsPerTilePart.ToString();
        }
        
        internal List<string> Validate()
        {
            var errors = new List<string>();
            
            if (Width < 0 || Height < 0)
                errors.Add("Tile dimensions must be non-negative");
            
            if (ReferenceX < 0 || ReferenceY < 0)
                errors.Add("Image reference point must be non-negative");
            
            if (TilingReferenceX < 0 || TilingReferenceY < 0)
                errors.Add("Tiling reference point must be non-negative");
            
            if (TilingReferenceX > ReferenceX || TilingReferenceY > ReferenceY)
                errors.Add("Tiling reference must not exceed image reference");
            
            if (PacketsPerTilePart < 0)
                errors.Add("Packets per tile-part must be non-negative");
            
            return errors;
        }
    }
    
    /// <summary>
    /// Wavelet filter types for JPEG 2000.
    /// </summary>
    public enum WaveletFilter
    {
        /// <summary>5-3 reversible filter (lossless compression).</summary>
        Reversible53,
        
        /// <summary>9-7 irreversible filter (lossy compression, better performance).</summary>
        Irreversible97
    }
    
    /// <summary>
    /// Configuration for wavelet transform.
    /// </summary>
    public class WaveletConfiguration
    {
        /// <summary>Gets or sets the wavelet filter type.</summary>
        public WaveletFilter Filter { get; set; } = WaveletFilter.Irreversible97;
        
        /// <summary>Gets or sets the number of decomposition levels.</summary>
        public int DecompositionLevels { get; set; } = 5;
        
        /// <summary>Gets or sets the code-block partition origin X.</summary>
        public int CodeBlockOriginX { get; set; } = 0;
        
        /// <summary>Gets or sets the code-block partition origin Y.</summary>
        public int CodeBlockOriginY { get; set; } = 0;

        /// <summary>
        /// Gets the filters of individual components, by component index; the other components use <see cref="Filter"/>. Each component's
        /// quantization type follows its filter (reversible for 5-3, otherwise the configured type).
        /// </summary>
        public Dictionary<int, WaveletFilter> ComponentFilters { get; } = new Dictionary<int, WaveletFilter>();

        /// <summary>
        /// Sets the filter of one component, for example 5-3 on the second component of an otherwise lossy encode. The encoder
        /// switches the component transform off when the first three components do not all use the same filter.
        /// </summary>
        public WaveletConfiguration WithComponentFilter(int component, WaveletFilter filter)
        {
            if (component < 0)
                throw new ArgumentOutOfRangeException(nameof(component), "Component index must be non-negative");
            ComponentFilters[component] = filter;
            return this;
        }
        
        /// <summary>
        /// Uses the 5-3 reversible filter (for lossless compression).
        /// </summary>
        public WaveletConfiguration UseReversible53()
        {
            Filter = WaveletFilter.Reversible53;
            return this;
        }
        
        /// <summary>
        /// Uses the 9-7 irreversible filter (for lossy compression).
        /// </summary>
        public WaveletConfiguration UseIrreversible97()
        {
            Filter = WaveletFilter.Irreversible97;
            return this;
        }
        
        /// <summary>
        /// Sets the number of wavelet decomposition levels.
        /// </summary>
        public WaveletConfiguration WithDecompositionLevels(int levels)
        {
            DecompositionLevels = levels;
            return this;
        }
        
        /// <summary>
        /// Sets the code-block partition origin.
        /// </summary>
        public WaveletConfiguration WithCodeBlockOrigin(int x, int y)
        {
            CodeBlockOriginX = x;
            CodeBlockOriginY = y;
            return this;
        }
        
        internal void ApplyTo(ParameterList pl)
        {
            pl["Ffilters"] = FilterSpec(Filter, ComponentFilters);
            pl["Wlev"] = DecompositionLevels.ToString();
            pl["Wcboff"] = $"{CodeBlockOriginX} {CodeBlockOriginY}";
        }
        
        internal static string FilterId(WaveletFilter filter) => filter == WaveletFilter.Reversible53 ? "w5x3" : "w9x7";

        /// <summary>The <c>Ffilters</c> value: the default filter, then <c>c&lt;index&gt; &lt;filter&gt;</c> for each component with its own.</summary>
        internal static string FilterSpec(WaveletFilter filter, IReadOnlyDictionary<int, WaveletFilter> componentFilters)
        {
            var spec = FilterId(filter);
            foreach (var cf in componentFilters.OrderBy(kv => kv.Key))
                spec += $" c{cf.Key} {FilterId(cf.Value)}";
            return spec;
        }

        /// <summary>
        /// Gives each component with its own filter the quantization type that filter needs: the 5-3 filter requires reversible
        /// quantization, the 9-7 filter a non-reversible one.
        /// </summary>
        internal void ApplyComponentQuantization(ParameterList pl, QuantizationType type)
        {
            if (ComponentFilters.Count == 0) return;
            var nonReversible = type == QuantizationType.Derived ? "derived" : "expounded";
            var global = type == QuantizationType.Reversible ? "reversible" : nonReversible;
            var qtype = global;
            foreach (var cf in ComponentFilters.OrderBy(kv => kv.Key))
            {
                var needed = cf.Value == WaveletFilter.Reversible53 ? "reversible" : nonReversible;
                if (needed != global) qtype += $" c{cf.Key} {needed}";
            }
            pl["Qtype"] = qtype;
        }
        
        internal List<string> Validate()
        {
            var errors = new List<string>();

            foreach (var component in ComponentFilters.Keys)
            {
                if (component < 0)
                    errors.Add($"Invalid component index for a wavelet filter: {component}");
            }
            
            if (DecompositionLevels < 0 || DecompositionLevels > 32)
                errors.Add("Decomposition levels must be between 0 and 32");
            
            if (CodeBlockOriginX < 0 || CodeBlockOriginX > 1)
                errors.Add("Code-block origin X must be 0 or 1");
            
            if (CodeBlockOriginY < 0 || CodeBlockOriginY > 1)
                errors.Add("Code-block origin Y must be 0 or 1");
            
            return errors;
        }
    }
    
    /// <summary>
    /// Quantization type for JPEG 2000.
    /// </summary>
    public enum QuantizationType
    {
        /// <summary>Reversible quantization (lossless).</summary>
        Reversible,
        
        /// <summary>Scalar derived quantization.</summary>
        Derived,
        
        /// <summary>Scalar expounded quantization.</summary>
        Expounded
    }
    
    /// <summary>
    /// Configuration for quantization.
    /// </summary>
    public class QuantizationConfiguration
    {
        /// <summary>Gets or sets the quantization type.</summary>
        public QuantizationType Type { get; set; } = QuantizationType.Expounded;
        
        /// <summary>Gets or sets the base quantization step size.</summary>
        public float BaseStepSize { get; set; } = 0.0078125f;
        
        /// <summary>Gets or sets the number of guard bits.</summary>
        public int GuardBits { get; set; } = 1;
        
        /// <summary>
        /// Uses reversible quantization (for lossless).
        /// </summary>
        public QuantizationConfiguration UseReversible()
        {
            Type = QuantizationType.Reversible;
            return this;
        }
        
        /// <summary>
        /// Uses derived quantization.
        /// </summary>
        public QuantizationConfiguration UseDerived()
        {
            Type = QuantizationType.Derived;
            return this;
        }
        
        /// <summary>
        /// Uses expounded quantization (default).
        /// </summary>
        public QuantizationConfiguration UseExpounded()
        {
            Type = QuantizationType.Expounded;
            return this;
        }
        
        /// <summary>
        /// Sets the base quantization step size.
        /// </summary>
        public QuantizationConfiguration WithBaseStepSize(float stepSize)
        {
            BaseStepSize = stepSize;
            return this;
        }
        
        /// <summary>
        /// Sets the number of guard bits.
        /// </summary>
        public QuantizationConfiguration WithGuardBits(int bits)
        {
            GuardBits = bits;
            return this;
        }
        
        internal void ApplyTo(ParameterList pl)
        {
            string qtypeValue;
            switch (Type)
            {
                case QuantizationType.Reversible:
                    qtypeValue = "reversible";
                    break;
                case QuantizationType.Derived:
                    qtypeValue = "derived";
                    break;
                case QuantizationType.Expounded:
                    qtypeValue = "expounded";
                    break;
                default:
                    qtypeValue = "expounded";
                    break;
            }
            pl["Qtype"] = qtypeValue;
            
            if (Type != QuantizationType.Reversible)
            {
                pl["Qstep"] = BaseStepSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            
            pl["Qguard_bits"] = GuardBits.ToString();
        }
        
        internal List<string> Validate()
        {
            var errors = new List<string>();
            
            if (Type != QuantizationType.Reversible && BaseStepSize <= 0)
                errors.Add("Base step size must be positive for non-reversible quantization");
            
            if (GuardBits < 0 || GuardBits > 7)
                errors.Add("Guard bits must be between 0 and 7");
            
            return errors;
        }
    }
    
    /// <summary>
    /// Progression order types for JPEG 2000.
    /// </summary>
    public enum ProgressionOrder
    {
        /// <summary>Layer-Resolution-Component-Position.</summary>
        LRCP,
        
        /// <summary>Resolution-Layer-Component-Position.</summary>
        RLCP,
        
        /// <summary>Resolution-Position-Component-Layer.</summary>
        RPCL,
        
        /// <summary>Position-Component-Resolution-Layer.</summary>
        PCRL,
        
        /// <summary>Component-Position-Resolution-Layer.</summary>
        CPRL
    }
    
    /// <summary>
    /// Configuration for progression order and quality layers.
    /// </summary>
    public class ProgressionConfiguration
    {
        /// <summary>Gets or sets the progression order.</summary>
        public ProgressionOrder Order { get; set; } = ProgressionOrder.LRCP;
        
        /// <summary>Gets the quality layers specification.</summary>
        public List<float> QualityLayers { get; } = new List<float>();

        /// <summary>Gets the progression orders of individual tiles, by tile index; the other tiles use <see cref="Order"/>.</summary>
        public Dictionary<int, ProgressionOrder> TileOrders { get; } = new Dictionary<int, ProgressionOrder>();

        /// <summary>
        /// Sets the progression order of one tile.
        /// </summary>
        public ProgressionConfiguration WithTileOrder(int tile, ProgressionOrder order)
        {
            if (tile < 0)
                throw new ArgumentOutOfRangeException(nameof(tile), "Tile index must be non-negative");
            TileOrders[tile] = order;
            return this;
        }
        
        /// <summary>
        /// Sets the progression order.
        /// </summary>
        public ProgressionConfiguration WithOrder(ProgressionOrder order)
        {
            Order = order;
            return this;
        }
        
        /// <summary>
        /// Adds quality layers with specified bitrates.
        /// </summary>
        public ProgressionConfiguration WithQualityLayers(params float[] layers)
        {
            QualityLayers.Clear();
            QualityLayers.AddRange(layers);
            return this;
        }
        
        internal static string AptypeId(ProgressionOrder order)
        {
            switch (order)
            {
                case ProgressionOrder.RLCP:
                    return "res";
                case ProgressionOrder.RPCL:
                    return "res-pos";
                case ProgressionOrder.PCRL:
                    return "pos-comp";
                case ProgressionOrder.CPRL:
                    return "comp-pos";
                default:
                    return "layer";
            }
        }

        /// <summary>The <c>Aptype</c> value: the default order, then <c>t&lt;index&gt; &lt;order&gt;</c> for each tile with its own.</summary>
        internal static string AptypeSpec(ProgressionOrder order, IReadOnlyDictionary<int, ProgressionOrder> tileOrders)
        {
            var spec = AptypeId(order);
            foreach (var to in tileOrders.OrderBy(kv => kv.Key))
                spec += $" t{to.Key} {AptypeId(to.Value)}";
            return spec;
        }

        internal void ApplyTo(ParameterList pl)
        {
            pl["Aptype"] = AptypeSpec(Order, TileOrders);
            
            if (QualityLayers.Count > 0)
            {
                var layerSpec = string.Join(" ", QualityLayers);
                pl["Alayers"] = layerSpec;
            }
        }
        
        internal List<string> Validate()
        {
            var errors = new List<string>();
            
            foreach (var layer in QualityLayers)
            {
                if (layer <= 0)
                    errors.Add("Quality layer bitrates must be positive");
            }

            foreach (var tile in TileOrders.Keys)
            {
                if (tile < 0)
                    errors.Add($"Invalid tile index for a progression order: {tile}");
            }
            
            return errors;
        }
    }
    
    /// <summary>
    /// Configuration for code-block settings.
    /// </summary>
    public class CodeBlockConfiguration
    {
        /// <summary>Gets or sets the code-block width (must be power of 2, 4-1024).</summary>
        public int Width { get; set; } = 64;
        
        /// <summary>Gets or sets the code-block height (must be power of 2, 4-1024).</summary>
        public int Height { get; set; } = 64;
        
        /// <summary>
        /// Sets the code-block size.
        /// </summary>
        public CodeBlockConfiguration SetSize(int width, int height)
        {
            Width = width;
            Height = height;
            return this;
        }
        
        internal void ApplyTo(ParameterList pl)
        {
            pl["Cblksiz"] = $"{Width} {Height}";
        }
        
        internal List<string> Validate()
        {
            var errors = new List<string>();
            
            if (!IsPowerOfTwo(Width) || Width < 4 || Width > 1024)
                errors.Add("Code-block width must be power of 2 between 4 and 1024");
            
            if (!IsPowerOfTwo(Height) || Height < 4 || Height > 1024)
                errors.Add("Code-block height must be power of 2 between 4 and 1024");
            
            if (Width * Height > 4096)
                errors.Add("Code-block area (width × height) must not exceed 4096");
            
            return errors;
        }
        
        private static bool IsPowerOfTwo(int n)
        {
            return n > 0 && (n & (n - 1)) == 0;
        }
    }
    
    /// <summary>
    /// Length calculation method for entropy coding.
    /// </summary>
    public enum LengthCalculation
    {
        /// <summary>Near optimal length calculation.</summary>
        NearOptimal,
        
        /// <summary>Lazy good length calculation.</summary>
        LazyGood,
        
        /// <summary>Lazy length calculation.</summary>
        Lazy
    }
    
    /// <summary>
    /// Termination type for entropy coding.
    /// </summary>
    public enum TerminationType
    {
        /// <summary>Near optimal termination.</summary>
        NearOptimal,
        
        /// <summary>Easy termination.</summary>
        Easy,
        
        /// <summary>Predictable termination.</summary>
        Predict,
        
        /// <summary>Full termination.</summary>
        Full
    }
    
    /// <summary>
    /// Configuration for entropy coding.
    /// </summary>
    public class EntropyCodingConfiguration
    {
        /// <summary>Gets or sets the length calculation method.</summary>
        public LengthCalculation LengthCalculation { get; set; } = LengthCalculation.NearOptimal;
        
        /// <summary>Gets or sets the termination type.</summary>
        public TerminationType Termination { get; set; } = TerminationType.NearOptimal;
        
        /// <summary>Gets or sets whether to use segmentation symbols.</summary>
        public bool SegmentationSymbol { get; set; } = false;
        
        /// <summary>Gets or sets whether to use causal context formation.</summary>
        public bool CausalMode { get; set; } = false;
        
        /// <summary>Gets or sets whether to reset MQ coder.</summary>
        public bool ResetMQ { get; set; } = false;
        
        /// <summary>Gets or sets whether to use bypass mode.</summary>
        public bool BypassMode { get; set; } = false;
        
        /// <summary>Gets or sets whether to use regular termination.</summary>
        public bool RegularTermination { get; set; } = false;
        
        internal void ApplyTo(ParameterList pl)
        {
            string lenCalcValue;
            switch (LengthCalculation)
            {
                case LengthCalculation.NearOptimal:
                    lenCalcValue = "near_opt";
                    break;
                case LengthCalculation.LazyGood:
                    lenCalcValue = "lazy_good";
                    break;
                case LengthCalculation.Lazy:
                    lenCalcValue = "lazy";
                    break;
                default:
                    lenCalcValue = "near_opt";
                    break;
            }
            pl["Clen_calc"] = lenCalcValue;
            
            string termTypeValue;
            switch (Termination)
            {
                case TerminationType.NearOptimal:
                    termTypeValue = "near_opt";
                    break;
                case TerminationType.Easy:
                    termTypeValue = "easy";
                    break;
                case TerminationType.Predict:
                    termTypeValue = "predict";
                    break;
                case TerminationType.Full:
                    termTypeValue = "full";
                    break;
                default:
                    termTypeValue = "near_opt";
                    break;
            }
            pl["Cterm_type"] = termTypeValue;
            
            pl["Cseg_symbol"] = SegmentationSymbol ? "on" : "off";
            pl["Ccausal"] = CausalMode ? "on" : "off";
            pl["CresetMQ"] = ResetMQ ? "on" : "off";
            pl["Cbypass"] = BypassMode ? "on" : "off";
            pl["Cterminate"] = RegularTermination ? "on" : "off";
        }
    }
    
    /// <summary>
    /// Configuration for error resilience features.
    /// </summary>
    public class ErrorResilienceConfiguration
    {
        /// <summary>Gets or sets whether to use SOP (Start of Packet) markers.</summary>
        public bool SOPMarkers { get; set; } = false;
        
        /// <summary>Gets or sets whether to use EPH (End of Packet Header) markers.</summary>
        public bool EPHMarkers { get; set; } = false;
        
        /// <summary>
        /// Enables SOP markers for error resilience.
        /// </summary>
        public ErrorResilienceConfiguration EnableSOPMarkers()
        {
            SOPMarkers = true;
            return this;
        }
        
        /// <summary>
        /// Enables EPH markers for error resilience.
        /// </summary>
        public ErrorResilienceConfiguration EnableEPHMarkers()
        {
            EPHMarkers = true;
            return this;
        }
        
        /// <summary>
        /// Enables both SOP and EPH markers.
        /// </summary>
        public ErrorResilienceConfiguration EnableAll()
        {
            SOPMarkers = true;
            EPHMarkers = true;
            return this;
        }
        
        internal void ApplyTo(ParameterList pl)
        {
            pl["Psop"] = SOPMarkers ? "on" : "off";
            pl["Peph"] = EPHMarkers ? "on" : "off";
        }
    }
}
