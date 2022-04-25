using System.Runtime.InteropServices;

namespace System.EnterpriseServices
{
    /// <summary>
    /// https://referencesource.microsoft.com/#System.EnterpriseServices/System/EnterpriseServices/XACTTRANSINFO.cs
    /// </summary>
    [ComVisible(false)]
    public struct XACTTRANSINFO
    {
        public int grfRMSupported;
        public int grfRMSupportedRetaining;
        public int grfTCSupported;
        public int grfTCSupportedRetaining;
        public int isoFlags;
        public int isoLevel;
        public BOID uow;
    }
}
