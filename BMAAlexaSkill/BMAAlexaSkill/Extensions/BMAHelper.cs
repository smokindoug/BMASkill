using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace BMAAlexaSkill.Extensions
{
    class BMAUtils
    {
        private static BMAUtils[] m_helpers;
        private bool m_isUsed;
        private string m_owner;
        private string m_cookies;
        private string m_volunteerPageContents;
        private bool m_isInitialized;

        static BMAUtils()
        {
            m_helpers = new BMAUtils[3];
            for (int i = 0; i < 3; i++)
                m_helpers[i] = new BMAUtils();
        }

        public static BMAUtils GetOrReserveBMAHelper(string owner, ILogger log)
        {
            foreach (BMAUtils helper in m_helpers)
                if (helper.m_isUsed && helper.m_owner == owner)
                {
                    log.LogInformation("Slot[{object}] found in use by owner: {owner}", helper.GetHashCode(), owner);
                    return helper;
                }

            foreach (BMAUtils helper in m_helpers)
                if (!helper.m_isUsed)
                {
                    helper.m_isUsed = true;
                    helper.m_owner = owner;
                    helper.Initialize(log);
                    log.LogInformation("" +
                        "Slot[{object}] given to owner: {owner}, and initialized[{init}",
                        helper.GetHashCode(), owner, helper.m_isInitialized);
                    return helper;
                }

            return null;
        }

        public bool RetrieveVolunteerHourPage(ILogger log)
        {
            using (WebClient client = new WebClient())
            {
                try
                {
                    m_volunteerPageContents = client.DownloadString("http://bma1.ca/record-volunteer-hours-c251.php");
                    m_cookies = client.ResponseHeaders["Set-Cookie"];
                    return true;
                }
                catch (WebException wExc)
                {
                    log.LogError(wExc, "failed to download BMA VolunteerHoursPage");
                    return false;
                }
            }
        }

        private void Initialize(ILogger log)
        {
            m_isInitialized = RetrieveVolunteerHourPage(log);
        }

    }
}
