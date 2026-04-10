using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text;

namespace GeriRemenyi.Oanda.V20.Client.Model
{
    [DataContract]
    // a zone is a list of candlesticks over time where there is group of exciting candles
    // called leg in followed a group of boring candles called base and followed again by
    // a group of exciting candles called leg out
    public class Zone : IEquatable<Zone>, IValidatableObject
    {
        [DataMember(Name = "type", EmitDefaultValue = false)]
        public ZoneType Type { get; set; }
        [DataMember(Name = "startTime", EmitDefaultValue = false)]
        public DateTime StartTime { get; set; }
        [DataMember(Name = "endTime", EmitDefaultValue = false)]
        public DateTime EndTime { get; set; }
        [DataMember(Name = "legInStartPrice", EmitDefaultValue = false)]
        public double LegInStartPrice { get; set; }
        [DataMember(Name = "legInEndPrice", EmitDefaultValue = false)]
        public double LegInEndPrice { get; set; }
        [DataMember(Name = "legOutStartPrice", EmitDefaultValue = false)]
        public double LegOutStartPrice { get; set; }
        [DataMember(Name = "legOutEndPrice", EmitDefaultValue = false)]
        public double LegOutEndPrice { get; set; }
        [DataMember(Name = "baseRangeHigh", EmitDefaultValue = false)]
        public double BaseRangeHigh { get; set; }
        [DataMember(Name = "baseRangeLow", EmitDefaultValue = false)]
        public double BaseRangeLow { get; set; }
        [DataMember(Name = "baseCandleCount", EmitDefaultValue = false)]
        public int BaseCandleCount { get; set; }
        [DataMember(Name = "freshness", EmitDefaultValue = false)]
        public ZoneFreshness Freshness { get; set; }
        [DataMember(Name = "worked", EmitDefaultValue = false)]
        public bool? Worked { get; set; }
        [DataMember(Name = "subZone", EmitDefaultValue = false)]
        public bool SubZone { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class Zone {\n");
            sb.Append("  Type: ").Append(Type).Append("\n");
            sb.Append("  StartTime: ").Append(StartTime).Append("\n");
            sb.Append("  EndTime: ").Append(EndTime).Append("\n");
            sb.Append("  LegInStartPrice: ").Append(LegInStartPrice).Append("\n");
            sb.Append("  LegInEndPrice: ").Append(LegInEndPrice).Append("\n");
            sb.Append("  LegOutStartPrice: ").Append(LegOutStartPrice).Append("\n");
            sb.Append("  LegOutEndPrice: ").Append(LegOutEndPrice).Append("\n");
            sb.Append("  BaseRangeHigh: ").Append(BaseRangeHigh).Append("\n");
            sb.Append("  BaseRangeLow: ").Append(BaseRangeLow).Append("\n");
            sb.Append("  BaseCandleCount: ").Append(BaseCandleCount).Append("\n");
            sb.Append("  Freshness: ").Append(Freshness).Append("\n");
            sb.Append("  Worked: ").Append(Worked).Append("\n");
            sb.Append("  SubZone: ").Append(SubZone).Append("\n");
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>
        /// Returns the JSON string presentation of the object
        /// </summary>
        /// <returns>JSON string presentation of the object</returns>
        public virtual string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Returns true if objects are equal
        /// </summary>
        /// <param name="input">Object to be compared</param>
        /// <returns>Boolean</returns>
        public override bool Equals(object input)
        {
            return this.Equals(input as Zone);
        }

        /// <summary>
        /// Returns true if Zone instances are equal
        /// </summary>
        /// <param name="input">Instance of Zone to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(Zone input)
        {
            if (input == null)
                return false;

            return
                (
                    this.StartTime == input.StartTime ||
                    this.StartTime.Equals(input.StartTime)
                ) &&
                (
                    this.EndTime == input.EndTime ||
                    this.EndTime.Equals(input.EndTime)
                ) &&
                (
                    this.BaseRangeHigh == input.BaseRangeHigh ||
                    this.BaseRangeHigh.Equals(input.BaseRangeHigh)
                ) &&
                (
                    this.BaseRangeLow == input.BaseRangeLow ||
                    this.BaseRangeLow.Equals(input.BaseRangeLow)
                );
        }

        /// <summary>
        /// Gets the hash code
        /// </summary>
        /// <returns>Hash code</returns>
        public override int GetHashCode()
        {
            unchecked // Overflow is fine, just wrap
            {
                int hashCode = 41;
                hashCode = hashCode * 59 + this.StartTime.GetHashCode();
                hashCode = hashCode * 59 + this.EndTime.GetHashCode();
                hashCode = hashCode * 59 + this.BaseRangeHigh.GetHashCode();
                hashCode = hashCode * 59 + this.BaseRangeLow.GetHashCode();
                return hashCode;
            }
        }

        /// <summary>
        /// To validate all properties of the instance
        /// </summary>
        /// <param name="validationContext">Validation context</param>
        /// <returns>Validation Result</returns>
        IEnumerable<System.ComponentModel.DataAnnotations.ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            yield break;
        }

    }
}
