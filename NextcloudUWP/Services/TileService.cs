using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace NextcloudUWP.Services
{
    public static class TileService
    {
        public static void UpdateTile(long quotaUsed, long quotaTotal)
        {
            var usedStr  = FormatHelper.FormatSize(quotaUsed);
            var totalStr = FormatHelper.FormatSize(quotaTotal);
            var pct      = quotaTotal > 0 ? (int)((double)quotaUsed / quotaTotal * 100) : 0;

            var xml = $@"<tile>
  <visual version=""4"">
    <binding template=""TileMedium"">
      <text hint-style=""captionSubtle"">Nextcloud</text>
      <text hint-style=""body"">{XmlEscape(usedStr)} used</text>
      <text hint-style=""captionSubtle"">of {XmlEscape(totalStr)} ({pct}%)</text>
    </binding>
    <binding template=""TileWide"">
      <text hint-style=""captionSubtle"">Nextcloud Storage</text>
      <text hint-style=""body"">{XmlEscape(usedStr)} of {XmlEscape(totalStr)} used</text>
      <text hint-style=""captionSubtle"">{pct}% of quota used</text>
    </binding>
  </visual>
</tile>";

            var doc = new XmlDocument();
            doc.LoadXml(xml);
            TileUpdateManager.CreateTileUpdaterForApplication()
                             .Update(new TileNotification(doc));
        }

        public static void UpdateBadge(int count)
        {
            var badgeXml = BadgeUpdateManager.GetTemplateContent(BadgeTemplateType.BadgeNumber);
            ((XmlElement)badgeXml.SelectSingleNode("/badge")).SetAttribute("value", count.ToString());
            BadgeUpdateManager.CreateBadgeUpdaterForApplication()
                              .Update(new BadgeNotification(badgeXml));
        }

        public static void ClearBadge()
        {
            BadgeUpdateManager.CreateBadgeUpdaterForApplication().Clear();
        }

        private static string XmlEscape(string s) => FormatHelper.XmlEscape(s);
    }
}
