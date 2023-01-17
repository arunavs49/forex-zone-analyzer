using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace GeriRemenyi.Oanda.V20.Client.Model
{
    /// <summary>
    /// ZonesResponse
    /// </summary>
    [DataContract]
    public partial class ZonesResponse :  IEquatable<ZonesResponse>, IValidatableObject
    {
        /// <summary>
        /// Gets or Sets Instrument
        /// </summary>
        [DataMember(Name="instrument", EmitDefaultValue=false)]
        public InstrumentName? Instrument { get; set; }
        /// <summary>
        /// Gets or Sets Granularity
        /// </summary>
        [DataMember(Name="granularity", EmitDefaultValue=false)]
        public CandlestickGranularity? Granularity { get; set; }
        /// <summary>
        /// Initializes a new instance of the <see cref="ZonesResponse" /> class.
        /// </summary>
        /// <param name="instrument">instrument.</param>
        /// <param name="granularity">granularity.</param>
        /// <param name="zones">The list of Zones that satisfy the request..</param>
        public ZonesResponse(InstrumentName? instrument = default(InstrumentName?), CandlestickGranularity? granularity = default(CandlestickGranularity?), List<Zone> zones = default(List<Zone>))
        {
            this.Instrument = instrument;
            this.Granularity = granularity;
            this.Zones = zones;
        }
        
        /// <summary>
        /// The list of Candlesticks that satisfy the request.
        /// </summary>
        /// <value>The list of Candlesticks that satisfy the request.</value>
        [DataMember(Name="zones", EmitDefaultValue=false)]
        public List<Zone> Zones { get; set; }

        /// <summary>
        /// Returns the string presentation of the object
        /// </summary>
        /// <returns>String presentation of the object</returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("class ZonesResponse {\n");
            sb.Append("  Instrument: ").Append(Instrument).Append("\n");
            sb.Append("  Granularity: ").Append(Granularity).Append("\n");
            sb.Append("  Zones: ").Append(Zones).Append("\n");
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
            return this.Equals(input as ZonesResponse);
        }

        /// <summary>
        /// Returns true if ZonesResponse instances are equal
        /// </summary>
        /// <param name="input">Instance of ZonesResponse to be compared</param>
        /// <returns>Boolean</returns>
        public bool Equals(ZonesResponse input)
        {
            if (input == null)
                return false;

            return 
                (
                    this.Instrument == input.Instrument ||
                    this.Instrument.Equals(input.Instrument)
                ) && 
                (
                    this.Granularity == input.Granularity ||
                    this.Granularity.Equals(input.Granularity)
                ) && 
                (
                    this.Zones == input.Zones ||
                    this.Zones != null &&
                    input.Zones != null &&
                    this.Zones.SequenceEqual(input.Zones)
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
                hashCode = hashCode * 59 + this.Instrument.GetHashCode();
                hashCode = hashCode * 59 + this.Granularity.GetHashCode();
                if (this.Zones != null)
                    hashCode = hashCode * 59 + this.Zones.GetHashCode();
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
