using System;
using Waher.Content;
using Waher.Script;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Exceptions;
using Waher.Script.Model;
using Waher.Script.Objects;
using Waher.Script.Units;

namespace TAG.Service.DockerRegistry.Script
{
    /// <summary>
    /// Convers a byte count number to a string with metric byte units.
    // </summary>
    public class ToMetricBytes : FunctionOneVariable
    {
        /// <summary>
        /// Convers a byte count number to a string with metric byte units.
        /// <summary>
        /// <param name="ByteCount">Byte count.</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        /// <param name="Expression">Expression.</param>
        public ToMetricBytes(ScriptNode ByteCount, int Start, int Length, Expression Expression)
            : base(ByteCount, Start, Length, Expression)
        {
        }

        /// <inheritdoc/>
        public override string FunctionName => "ToMetricBytes";

        /// <inheritdoc/>
        public override IElement Evaluate(IElement Argument, Variables Variables)
        {
            if (!(Argument.AssociatedObjectValue is double ByteCount))
                throw new Exception("First argument should be an integer number of bytes.");

            if (!(Math.Floor(ByteCount) == ByteCount))
                throw new Exception("First argument needs to be a whole number");

            return new StringValue(ToBinaryBytes(ByteCount));
        }

        static string ToBinaryBytes(double value)
        {
            byte NumberDecimal = 0;
            Prefix Unit = Prefix.None;

            while (value >= 1024)
            {
                value /= 1024;
                NumberDecimal = 2;
                Unit += 3;
            }

            return $"{CommonTypes.Encode(value, NumberDecimal)} {Prefixes.ToString(Unit)}B";
        }
    }
}
