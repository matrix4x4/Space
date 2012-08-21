﻿using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;
using Engine.Serialization;

namespace Engine.Math
{
    /// <summary>
    /// Represents an interval of the specified type.
    /// </summary>
    /// <typeparam name="T">The interval type.</typeparam>
    [TypeConverter(typeof(IntervalConverter))]
    public abstract class Interval<T> : IPacketizable, IHashable where T : IComparable<T>, IEquatable<T>
    {
        #region Properties
        
        /// <summary>
        /// The low endpoint of the interval.
        /// </summary>
        [Description("The lower inclusive bound of the interval.")]
        public T Low
        {
            get { return _low; }
            set { SetTo(value, _high); }
        }

        /// <summary>
        /// The high endpoint of the interval.
        /// </summary>
        [Description("The upper inclusive bound of the interval.")]
        public T High
        {
            get { return _high; }
            set { SetTo(_low, value); }
        }

        #endregion

        #region Backing fields

        private T _low;

        private T _high;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="Interval&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="low">The low endpoint.</param>
        /// <param name="high">The high endpoint.</param>
        protected Interval(T low, T high)
        {
            SetTo(low, high);
        }

        /// <summary>
        /// For serialization.
        /// </summary>
        protected Interval()
        {
        }

        #endregion

        #region Methods
        
        /// <summary>
        /// Sets the interval endpoints to the specified values.
        /// </summary>
        /// <param name="low">The low endpoint.</param>
        /// <param name="high">The high endpoint.</param>
        /// <exception cref="ArgumentException">If low is larger than high.</exception>
        public void SetTo(T low, T high)
        {
            if (low.CompareTo(high) > 0)
            {
                throw new ArgumentException("Invalid interval, the lower endpoint must be less or equal to the higher endpoint.", "low");
            }
            _low = low;
            _high = high;
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>The packet after writing.</returns>
        public abstract Packet Packetize(Packet packet);

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public abstract void Depacketize(Packet packet);

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public abstract void Hash(Hasher hasher);

        #endregion

        #region ToString

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "[" + _low + ", " + _high + "]";
        }

        #endregion
    }

    public sealed class IntInterval : Interval<int>
    {
        #region Constants

        /// <summary>
        /// Default 'zero' value for an interval.
        /// </summary>
        public static IntInterval Zero
        {
            get { return ConstZero; }
        }

        /// <summary>
        /// Internal field to avoid manipulation.
        /// </summary>
        private static readonly IntInterval ConstZero = new IntInterval(0, 0);

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="IntInterval"/> class.
        /// </summary>
        /// <param name="low">The low endpoint.</param>
        /// <param name="high">The high endpoint.</param>
        public IntInterval(int low, int high)
        {
            SetTo(low, high);
        }

        /// <summary>
        /// For serialization.
        /// </summary>
        public IntInterval()
        {
        }
        
        #endregion

        #region Serialization

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>The packet after writing.</returns>
        public override Packet Packetize(Packet packet)
        {
            return packet.Write(Low).Write(High);
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            var low = packet.ReadInt32();
            var high = packet.ReadInt32();
            SetTo(low, high);
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            hasher.Put(Low).Put(High);
        }

        #endregion
    }

    public sealed class FloatInterval : Interval<float>
    {
        #region Constants

        /// <summary>
        /// Default 'zero' value for an interval.
        /// </summary>
        public static FloatInterval Zero
        {
            get { return ConstZero; }
        }

        /// <summary>
        /// Internal field to avoid manipulation.
        /// </summary>
        private static readonly FloatInterval ConstZero = new FloatInterval(0f, 0f);

        #endregion
        
        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatInterval"/> class.
        /// </summary>
        /// <param name="low">The low endpoint.</param>
        /// <param name="high">The high endpoint.</param>
        public FloatInterval(float low, float high)
        {
            SetTo(low, high);
        }

        /// <summary>
        /// For serialization.
        /// </summary>
        public FloatInterval()
        {
        }
        
        #endregion

        #region Serialization

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>The packet after writing.</returns>
        public override Packet Packetize(Packet packet)
        {
            return packet.Write(Low).Write(High);
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            var low = packet.ReadSingle();
            var high = packet.ReadSingle();
            SetTo(low, high);
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            hasher.Put(Low).Put(High);
        }

        #endregion
    }

    public sealed class DoubleInterval : Interval<double>
    {
        #region Constants

        /// <summary>
        /// Default 'zero' value for an interval.
        /// </summary>
        public static DoubleInterval Zero
        {
            get { return ConstZero; }
        }

        /// <summary>
        /// Internal field to avoid manipulation.
        /// </summary>
        private static readonly DoubleInterval ConstZero = new DoubleInterval(0.0, 0.0);

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="FloatInterval"/> class.
        /// </summary>
        /// <param name="low">The low endpoint.</param>
        /// <param name="high">The high endpoint.</param>
        public DoubleInterval(double low, double high)
        {
            SetTo(low, high);
        }

        /// <summary>
        /// For serialization.
        /// </summary>
        public DoubleInterval()
        {
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Write the object's state to the given packet.
        /// </summary>
        /// <param name="packet">The packet to write the data to.</param>
        /// <returns>The packet after writing.</returns>
        public override Packet Packetize(Packet packet)
        {
            return packet.Write(Low).Write(High);
        }

        /// <summary>
        /// Bring the object to the state in the given packet.
        /// </summary>
        /// <param name="packet">The packet to read from.</param>
        public override void Depacketize(Packet packet)
        {
            var low = packet.ReadDouble();
            var high = packet.ReadDouble();
            SetTo(low, high);
        }

        /// <summary>
        /// Push some unique data of the object to the given hasher,
        /// to contribute to the generated hash.
        /// </summary>
        /// <param name="hasher">The hasher to push data to.</param>
        public override void Hash(Hasher hasher)
        {
            hasher.Put(Low).Put(High);
        }

        #endregion
    }

    /// <summary>
    /// Custom converter for intervals for text editing.
    /// </summary>
    public sealed class IntervalConverter : ExpandableObjectConverter
    {
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return base.CanConvertTo(context, destinationType) || destinationType == typeof(string);
        }

        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return base.CanConvertFrom(context, sourceType) || sourceType == typeof(string);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string))
            {
                if (value is DoubleInterval)
                {
                    var i = (DoubleInterval)value;
                    if (i.Low == i.High)
                    {
                        return i.Low.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return i.Low.ToString(CultureInfo.InvariantCulture) + " to " +
                               i.High.ToString(CultureInfo.InvariantCulture);
                    }
                }
                else if (value is FloatInterval)
                {
                    var i = (FloatInterval)value;
                    if (i.Low == i.High)
                    {
                        return i.Low.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return i.Low.ToString(CultureInfo.InvariantCulture) + " to " +
                               i.High.ToString(CultureInfo.InvariantCulture);
                    }
                }
                else if (value is IntInterval)
                {
                    var i = (IntInterval)value;
                    if (i.Low == i.High)
                    {
                        return i.Low.ToString(CultureInfo.InvariantCulture);
                    }
                    else
                    {
                        return i.Low.ToString(CultureInfo.InvariantCulture) + " to " +
                               i.High.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }

        private static readonly Regex IntervalPattern = new Regex(@"
            ^\s*           # Complete line, ignore leading whitespace.
            (?<low>        # Read the low value, which must be a number.
                -?[0-9]+
                (
                    \.[0-9]+  # Optionally a floating point value.
                )?
            )
            (
                \s+to\s+    # Optionally a high value, which must be separated by a 'to'.
                (?<high>
                    -?[0-9]+
                    (
                        \.[0-9]+    # Again, optionally as floating point.
                    )?
                )
            )?
            \s*$     # Skip trailing whitespace",
             RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
            {
                // Parse the content.
                //bool round = input.Xml.GetAttribute("round") != null && bool.Parse(input.Xml.GetAttribute("round"));
                var match = IntervalPattern.Match((string)value);
                if (match.Success)
                {
                    // Now get the numeric value for the attribute.
                    var low = match.Groups["low"].Value;
                    var high = match.Groups["high"].Success ? match.Groups["high"].Value : low;

                    if (context.PropertyDescriptor.PropertyType == typeof(DoubleInterval))
                    {
                        return new DoubleInterval(double.Parse(low, CultureInfo.InvariantCulture),
                            double.Parse(high, CultureInfo.InvariantCulture));
                    }
                    else if (context.PropertyDescriptor.PropertyType == typeof(FloatInterval))
                    {
                        return new FloatInterval(float.Parse(low, CultureInfo.InvariantCulture),
                            float.Parse(high, CultureInfo.InvariantCulture));
                    }
                    else if (context.PropertyDescriptor.PropertyType == typeof(IntInterval))
                    {
                        return new IntInterval(int.Parse(low, CultureInfo.InvariantCulture),
                            int.Parse(high, CultureInfo.InvariantCulture));
                    }
                }
                else
                {
                    throw new ArgumentException("Invalid format.", "value");
                }
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}