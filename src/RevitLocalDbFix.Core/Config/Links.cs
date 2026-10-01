namespace RevitLocalDbFix.Core.Config
{
    /// <summary>
    /// All external URLs used by the tool (SPEC §1.3). This is the single place to change links.
    /// </summary>
    public static class Links
    {
        public const string ART_REVIT_HANG =
            "https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Revit-Journal-records-SQLLocalDB-instance-is-malfunctioning.html";

        public const string ART_LOCALDB_INVESTIGATE =
            "https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/Advance-Steel-is-not-able-to-start-Failed-to-connect-to-all-AS-databases.html";

        public const string DL_LOCALDB_2014 =
            "https://download.microsoft.com/download/3/9/F/39F968FA-DEBB-4960-8F9E-0E7BB3035959/ENU/x64/SqlLocalDB.msi";

        public const string DL_LOCALDB_2019 =
            "https://www.microsoft.com/en-us/download/details.aspx?id=101064";

        public const string DL_SECTOR_REG =
            "https://help.autodesk.com/sfdcarticles/attachments/SetSectorSize.reg";

        /// <summary>[待实测 SPEC §13-3] SQL 2019 Express SSEI bootstrapper direct link, to be verified.</summary>
        public const string DL_LOCALDB_2019_SSEI_CANDIDATE =
            "https://go.microsoft.com/fwlink/?linkid=866658";

        public const string TITLE_ART_REVIT_HANG =
            "Revit: Hangs or Crashes Due to SQLLocalDB Issues When Working with Models and Steel Connections";

        public const string TITLE_ART_LOCALDB_INVESTIGATE =
            "Investigating SQL Server LocalDB installation when working with Advance Steel / Revit";

        public static string GetUrl(string id)
        {
            switch (id)
            {
                case "ART_REVIT_HANG": return ART_REVIT_HANG;
                case "ART_LOCALDB_INVESTIGATE": return ART_LOCALDB_INVESTIGATE;
                case "DL_LOCALDB_2014": return DL_LOCALDB_2014;
                case "DL_LOCALDB_2019": return DL_LOCALDB_2019;
                case "DL_SECTOR_REG": return DL_SECTOR_REG;
                default: return null;
            }
        }

        public static string GetTitle(string id)
        {
            switch (id)
            {
                case "ART_REVIT_HANG": return TITLE_ART_REVIT_HANG;
                case "ART_LOCALDB_INVESTIGATE": return TITLE_ART_LOCALDB_INVESTIGATE;
                default: return GetUrl(id);
            }
        }
    }
}
