/// <summary>**************************************************************************
/// 
/// 
/// Copyright Eastman Kodak Company, 343 State Street, Rochester, NY 14650
/// $Date $
/// ***************************************************************************
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
using ICCProfile = CoreJ2K.Icc.ICCProfile;
using io_RandomAccessIO = CoreJ2K.j2k.io.RandomAccessIO;

namespace CoreJ2K.Color.Boxes
{

    /// <summary> This class maps the components in the codestream
    /// to channels in the image.  It models the Component
    /// Mapping box in the JP2 header.
    /// 
    /// </summary>
    /// <version> 	1.0
    /// </version>
    /// <author> 	Bruce A. Kern
    /// </author>
    public sealed class ChannelDefinitionBox : JP2Box
    {
        public int NDefs { get; private set; }

        private readonly Dictionary<int, int[]> definitions = new Dictionary<int, int[]>();

        // every definition in file order: a channel may be defined more than once (an opacity channel for several colours)
        private readonly List<(int Cn, int Typ, int Asoc)> entries = new List<(int, int, int)>();

        /// <summary> Construct a ChannelDefinitionBox from an input image.</summary>
        /// <param name="in">RandomAccessIO jp2 image
        /// </param>
        /// <param name="boxStart">offset to the start of the box in the image
        /// </param>
        /// <exception cref="IOException,">ColorSpaceException 
        /// </exception>
        public ChannelDefinitionBox(io_RandomAccessIO inStream, int boxStart) : base(inStream, boxStart)
        {
            readBox();
        }

        /// <summary>Analyze the box content. </summary>
        private void readBox()
        {

            var bfr = new byte[8];

            inStream.seek(dataStart);
            inStream.readFully(bfr, 0, 2);
            NDefs = ICCProfile.GetShort(bfr, 0) & 0x0000ffff;

            var offset = dataStart + 2;
            inStream.seek(offset);
            for (var i = 0; i < NDefs; ++i)
            {
                inStream.readFully(bfr, 0, 6);
                int channel = ICCProfile.GetShort(bfr, 0);
                var channel_def = new int[3];
                channel_def[0] = GetCn(bfr);
                channel_def[1] = GetTyp(bfr);
                channel_def[2] = GetAsoc(bfr);
                definitions[channel_def[0]] = channel_def;
                entries.Add((channel_def[0], channel_def[1], channel_def[2]));
            }
        }

        /// <summary>
        /// The order in which the channels are put out: the colour channels in the order of the colours they stand for (association
        /// 1, 2, 3 ...), then the opacity channels, then the premultiplied-opacity channels, then any channel the box does not
        /// define. The result holds each of the <paramref name="channelCount"/> channels exactly once, so it can be used to
        /// index them: definitions of channels that do not exist are ignored, and so are repeats.
        /// </summary>
        /// <param name="channelCount">The number of channels the image has.</param>
        public int[] GetOutputOrder(int channelCount)
        {
            var order = new List<int>(channelCount);
            var taken = new bool[channelCount];

            void Take(IEnumerable<(int Cn, int Typ, int Asoc)> group)
            {
                foreach (var entry in group)
                {
                    if (entry.Cn < 0 || entry.Cn >= channelCount || taken[entry.Cn]) continue;
                    taken[entry.Cn] = true;
                    order.Add(entry.Cn);
                }
            }

            // association 0 means the whole image, which is no position among the colours: such a channel goes after the others
            Take(entries.Where(e => e.Typ == 0).OrderBy(e => e.Asoc == 0 ? int.MaxValue : e.Asoc));
            Take(entries.Where(e => e.Typ == 1).OrderBy(e => e.Asoc));
            Take(entries.Where(e => e.Typ == 2).OrderBy(e => e.Asoc));
            for (var c = 0; c < channelCount; c++)
                if (!taken[c]) order.Add(c);
            return order.ToArray();
        }

        /* Return the channel association. */
        public int GetCn(int asoc)
        {
            IEnumerator<int> keys = definitions.Keys.GetEnumerator();
            while (keys.MoveNext())
            {
                var bfr = definitions[keys.Current];
                if (asoc == GetAsoc(bfr))
                    return GetCn(bfr);
            }
            return asoc;
        }

        /* Return the channel type. */
        public int GetTyp(int channel)
        {
            var bfr = definitions[channel];
            return GetTyp(bfr);
        }

        /* Return the associated channel of the association. */
        public int GetAsoc(int channel)
        {
            var bfr = definitions[channel];
            return GetAsoc(bfr);
        }


        /// <summary>Return a suitable String representation of the class instance. </summary>
        public override string ToString()
        {
            var rep = new System.Text.StringBuilder("[ChannelDefinitionBox ").Append(Environment.NewLine).Append("  ");
            rep.Append("ndefs= ").Append(Convert.ToString(NDefs));

            IEnumerator<int> keys = definitions.Keys.GetEnumerator();
            while (keys.MoveNext())
            {
                var bfr = definitions[keys.Current];
                rep.Append(Environment.NewLine).Append("  ").Append("Cn= ").Append(Convert.ToString(GetCn(bfr))).Append(", ").Append("Typ= ").Append(Convert.ToString(GetTyp(bfr))).Append(", ").Append("Asoc= ").Append(Convert.ToString(GetAsoc(bfr)));
            }

            rep.Append("]");
            return rep.ToString();
        }

        /// <summary>Return the channel from the record.</summary>
        private int GetCn(byte[] bfr)
        {
            return ICCProfile.GetShort(bfr, 0);
        }

        /// <summary>Return the channel type from the record.</summary>
        private int GetTyp(byte[] bfr)
        {
            return ICCProfile.GetShort(bfr, 2);
        }

        /// <summary>Return the associated channel from the record.</summary>
        private int GetAsoc(byte[] bfr)
        {
            return ICCProfile.GetShort(bfr, 4);
        }

        private int GetCn(int[] bfr)
        {
            return bfr[0];
        }

        private int GetTyp(int[] bfr)
        {
            return bfr[1];
        }

        private int GetAsoc(int[] bfr)
        {
            return bfr[2];
        }
        static ChannelDefinitionBox()
        {
            {
                type = 0x63646566;
            }
        }
    }
}