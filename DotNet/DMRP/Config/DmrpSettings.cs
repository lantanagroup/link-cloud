namespace LantanaGroup.Link.DMRP.Config
{
    /// <summary>
    /// Settings that control the DMRP module hosted by the Tenant service.
    /// </summary>
    public class DmrpSettings
    {
        public const string ConfigSectionName = "DMRP";

        /// <summary>
        /// When false, none of the DMRP controllers, persistence or scheduling behavior is registered
        /// and the host continues to perform facility dQM reporting on its own.
        /// </summary>
        public bool Enabled { get; set; }
    }
}
