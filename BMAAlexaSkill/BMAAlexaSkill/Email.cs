using System;
using System.Collections.Generic;
using System.Text;

namespace BMAAlexaSkill
{
    class Email
    {
        public bool SendMail(string mail)
        {
            System.Net.Mail.SmtpClient smtp = new System.Net.Mail.SmtpClient();
            return smtp == null ? false : true;
        }
    }
}
