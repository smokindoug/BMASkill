using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace BMAUtils
{

    public static class BMAProcessor
    {
        public static IAlexaHelper s_AlexaHelper = new AmazonAlexaHelper();
        public static SkillResponse processRequest(SkillRequest request, ILogger log)
        {
            BMAHelper helper = null;
            var name = s_AlexaHelper.RetrieveAlexaClientName(log, request.Context.System.ApiAccessToken);
            if (name != null)
            {
                helper = BMAUtils.GetOrReserveBMAHelper(JsonConvert.DeserializeObject<string>(name), log);
            }
            else
            {
                return new PermissionsProcessor(request, log).processRequest();
            }

            if (request.Session.Attributes == null)
            {
                request.Session.Attributes = new Dictionary<string, object>();
            }
            request.Session.Attributes["account_first"] = helper.m_first;
            request.Session.Attributes["account_last"] = helper.m_rest;
            BaseProcessor processor = null;
            if (request.Request is LaunchRequest launchRequest)
            {
                processor = new OpenProcessor(request, log);
            }
            else if (request.Request is IntentRequest intentRequest)
            {
                if (intentRequest.Intent.Name == "reporthours")
                {
                    processor = new ReportHoursProcessor(request, log);
                }
                else if (intentRequest.Intent.Name == "addhours")
                {
                    processor = new AddHoursProcessor(request, log);
                }
                else if (intentRequest.Intent.Name == "designate")
                {
                    processor = new DesignateProcessor(request, log);
                }
                else if (intentRequest.Intent.Name == "AMAZON.HelpIntent")
                {
                    processor = new HelpProcessor(request, log);
                }
                else if (
                    intentRequest.Intent.Name == "AMAZON.CancelIntent" || 
                    intentRequest.Intent.Name == "AMAZON.StopIntent")
                {
                    processor = new CancelProcessor(request, log, "<speak>Ok.</speak>");
                }
                else
                {
                    processor = new CannotUnderstandProcessor(request, log);
                }
            }
            else if (request.Request is SessionEndedRequest sessionEndedRequest)
            {
                processor = new SessionEndedProcessor(request, log);
            }
            return processor.processRequest();
        }
    }

    public interface IAlexaHelper
    {
        string RetrieveAlexaClientName(ILogger log, string apiToken);
    }

    internal class AmazonAlexaHelper : IAlexaHelper
    {
        public string RetrieveAlexaClientName(ILogger log, string apiToken)
        {
            using (WebClient client = new WebClient())
            {
                string bearerToken = String.Format("Bearer {0}", apiToken);
                client.Headers[HttpRequestHeader.Authorization] = bearerToken;

                try
                {
                    var rtn = client.DownloadString("https://api.amazonalexa.com/v2/accounts/~current/settings/Profile.name");
                    log.LogInformation(rtn);
                    return rtn;
                }
                catch (WebException wExc)
                {
                    log.LogError(wExc, "failed to get name from token {0}", apiToken);
                }
            }
            return null;
        }
    }

    abstract class BaseProcessor
    {
        protected SkillRequest LastRequest { get; set; }
        protected ILogger m_log;

        internal BaseProcessor(SkillRequest request, ILogger log)
        {
            LastRequest = request;
            m_log = log;
        }
        internal virtual SkillResponse processRequest()
        {
            throw new NotImplementedException();
        }
    }

    internal class OpenProcessor : BaseProcessor
    {
        internal OpenProcessor(SkillRequest request, ILogger log) : base(request, log) { }

        internal override SkillResponse processRequest()
        {
            SkillResponse response = null;
            m_log.LogInformation("Session started");
            var speech = new SsmlOutputSpeech();
            speech.Ssml =
                "<speak>Welcome to the <say-as interpret-as=\"spell-out\">bma</say-as>  hours reporting skill. How can I help?</speak>";
            SsmlOutputSpeech reprompt = null;
            reprompt = new SsmlOutputSpeech();
            reprompt.Ssml = "<speak>Hello, you can ask me something like <prosody rate=\"slow\">add hours.</prosody></speak>";
            response = ResponseBuilder.Ask(
                speech,
                new Reprompt
                {
                    OutputSpeech = reprompt
                });
            return response;
        }
    }

    internal class ReportHoursProcessor : BaseProcessor
    {
        internal ReportHoursProcessor(SkillRequest request, ILogger log) : base(request, log) { }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Session started");
            string first = null;
            string last = null;
            bool isDesignate = false;
            if (LastRequest.Session.Attributes.ContainsKey("designated_last"))
            {
                isDesignate = true;
            }
            else
            {
                last = (string)LastRequest.Session.Attributes["account_last"];
                first = (string)LastRequest.Session.Attributes["account_first"];
            }
            var speech = new SsmlOutputSpeech();

            if (isDesignate)
            {
                speech.Ssml = "<speak>I cannot report hours for a designated volunteer. If you cancel I will reset to the account owner.</speak>";
                SkillResponse response =ResponseBuilder.Tell(speech);
                response.Response.ShouldEndSession = false;
                return response;
            }
            speech.Ssml =
                "<speak>You have <say-as interpret-as=\"cardinal\">10</say-as> volunteer hours.</speak>";
            SsmlOutputSpeech reprompt = null;
            reprompt = new SsmlOutputSpeech();
            reprompt.Ssml = "<speak>I am ready for more requests. You can ask me something like <prosody rate=\"slow\">add hours or report hours.</prosody></speak>";
            return ResponseBuilder.Ask(
                speech,
                new Reprompt
                {
                    OutputSpeech = reprompt
                });
        }
    }
    internal class AddHoursProcessor : BaseProcessor
    {
        private IntentRequest m_intentRequest;
        internal AddHoursProcessor(SkillRequest request, ILogger log) : base(request, log)
        {
            m_intentRequest = request.Request as IntentRequest;
        }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Add hours");

            if (m_intentRequest.Intent.ConfirmationStatus == "DENIED")
            {
                return new CancelProcessor(LastRequest, m_log, "<speak>Add hours request was cancelled.</speak>").processRequest();
            }
            string hours = null;
            string task = null;

            foreach (KeyValuePair<string, Alexa.NET.Request.Slot> value in m_intentRequest.Intent.Slots)
            {
                if (value.Key == "hours")
                    hours = value.Value.Value;
                if (value.Key == "task")
                    task = value.Value.Value;
            }

            string first = LastRequest.Session.Attributes["account_first"] as string;
            string last = LastRequest.Session.Attributes["account_last"] as string;
            bool isDesignated = false;
            if (LastRequest.Session.Attributes.ContainsKey("designated_last"))
            {
                isDesignated = true;
                first = LastRequest.Session.Attributes["designated_first"] as string;
                last = LastRequest.Session.Attributes["designated_last"] as string;
            }

            var speech = new SsmlOutputSpeech();

            StringBuilder sb = new StringBuilder("<speak>");
            sb.Append("I have added <say-as interpret-as=\"cardinal\">");
            sb.Append(hours);
            sb.Append("</say-as> hours for the task <prosody rate=\"slow\">");
            sb.Append(task);
            sb.Append("</prosody>");
            if (isDesignated)
            {
                sb.Append(". For the volunteer ");
                sb.Append(first);
                sb.Append(" ");
                sb.Append(last);
            }
            sb.Append(". Thank you");
            sb.Append("</speak>");

            speech.Ssml = sb.ToString();

            SsmlOutputSpeech reprompt = null;
            reprompt = new SsmlOutputSpeech();
            reprompt.Ssml = "<speak>If you want to continue, please ask me another b.m.a request.</speak>";
            return ResponseBuilder.Ask(
                speech,
                new Reprompt
                {
                    OutputSpeech = reprompt
                });
        }
    }

    internal class DesignateProcessor : BaseProcessor
    {
        private IntentRequest m_intentRequest;
        internal DesignateProcessor(SkillRequest request, ILogger log) : base(request, log) {
            m_intentRequest = request.Request as IntentRequest;
        }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Designate");

            if (m_intentRequest.Intent.ConfirmationStatus == "DENIED")
            {
                return new CancelProcessor(LastRequest, m_log, "<speak>Cancelled.</speak>").processRequest();
            }
            string first = null;
            string last = null;
            foreach (KeyValuePair<string, Alexa.NET.Request.Slot> value in m_intentRequest.Intent.Slots)
            {
                if (value.Key == "first_name")
                    first = value.Value.Value;
                if (value.Key == "last_name")
                    last = value.Value.Value;
            }
            var speech = new SsmlOutputSpeech();
            speech.Ssml =
                "<speak>I have made <prosody rate=\"slow\">" + 
                first +
                " " 
                +
                last + 
                "</prosody> designated volunteer. You can ask me to add hours for them.</speak>";
            SsmlOutputSpeech reprompt = null;
            reprompt = new SsmlOutputSpeech();
            reprompt.Ssml = "<speak>If you want to continue, please ask me to add hours for " +
                first + " " + last + ".</speak>";

            if (LastRequest.Session.Attributes == null)
            {
                LastRequest.Session.Attributes = new Dictionary<string, object>();
            }
            LastRequest.Session.Attributes["designated_first"] = first;
            LastRequest.Session.Attributes["designated_last"] = last;
            return ResponseBuilder.Ask(
                speech,
                new Reprompt
                {
                    OutputSpeech = reprompt
                }, 
                LastRequest.Session);
        }
    }

    internal class CannotUnderstandProcessor : BaseProcessor
    {
        internal CannotUnderstandProcessor(SkillRequest request, ILogger log) : base(request, log) { }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Do not understand the intent {name}", ((IntentRequest)LastRequest.Request).Intent.Name);
            var speech = new SsmlOutputSpeech();
            speech.Ssml = "<speak>Sorry, I did not understand that. Goodbye.</speak>";
            SkillResponse response = ResponseBuilder.Tell(speech);
            response.Response.ShouldEndSession = true;
            return response;
        }
    }

    internal class CancelProcessor : BaseProcessor
    {
        private string m_msg;
        internal CancelProcessor(SkillRequest request, ILogger log, string msg) : base(request, log)
        {
            m_msg = msg;
        }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Cancel");

            LastRequest.Session.Attributes.Remove("designated_last");
            LastRequest.Session.Attributes.Remove("designated_first");

            var speech = new SsmlOutputSpeech();
            speech.Ssml = m_msg;
            SkillResponse response = ResponseBuilder.Tell(speech, LastRequest.Session);
            response.Response.ShouldEndSession = false;
            return response;
        }
    }

    internal class SessionEndedProcessor : BaseProcessor
    {
        internal SessionEndedProcessor(SkillRequest request, ILogger log) : base(request, log)  { }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Session ended");
            return ResponseBuilder.Empty();
        }
    }

    internal class HelpProcessor : BaseProcessor
    {
        internal HelpProcessor(SkillRequest request, ILogger log) : base(request, log) { }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Help intent");
            var speech = new SsmlOutputSpeech();
            speech.Ssml = "<speak>You can ask me to add volunteer hours, report volunteer hours or designate volunteer.</speak>";
            SkillResponse response = ResponseBuilder.Tell(speech);
            response.Response.ShouldEndSession = false;
            return response;
        }
    }

    internal class PermissionsProcessor : BaseProcessor
    {
        internal PermissionsProcessor(SkillRequest request, ILogger log) : base (request, log) { }

        internal override SkillResponse processRequest()
        {
            m_log.LogInformation("Account owner has not granted permission");
            var speech = new SsmlOutputSpeech();
            speech.Ssml = "<speak>I could not open the skill. Have you granted permission to the b.m.a. Volunteer Skill?</speak>";
            SkillResponse response = ResponseBuilder.Tell(speech);
            response.Response.ShouldEndSession = true;
            return response;
        }
    }
}