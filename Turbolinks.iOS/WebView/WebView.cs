﻿namespace Turbolinks.iOS
{
    using System;
    using System.Linq;
    using System.Reflection;
    using CoreGraphics;
    using Foundation;
    using Newtonsoft.Json;
    using ObjCRuntime;
    using WebKit;

    public class WebView : WKWebView, IWKScriptMessageHandler
    {
        IWebViewDelegate _delegate;
        IWebViewPageLoadDelegate _pageLoadDelegate;
        IWebViewVisitDelegate _visitDelegate;

        public WebView(WKWebViewConfiguration configuration) : base(CGRect.Empty, configuration)
        {
            var url = NSBundle.MainBundle.GetUrlForResource("WebView", "js");

            var data = NSData.FromUrl(url);

            var source = new NSString(data, NSStringEncoding.UTF8);

            var userScript = new WKUserScript(source, WKUserScriptInjectionTime.AtDocumentEnd, true);
            configuration.UserContentController.AddUserScript(userScript);
            configuration.UserContentController.AddScriptMessageHandler(this, "turbolinks");

            TranslatesAutoresizingMaskIntoConstraints = false;
            ScrollView.DecelerationRate = UIKit.UIScrollView.DecelerationRateNormal;
        }

        public IWebViewDelegate Delegate
        {
            get => _delegate;
            set => _delegate = value;
        }

		public IWebViewPageLoadDelegate PageLoadDelegate
		{
			get => _pageLoadDelegate;
			set => _pageLoadDelegate = value;
		}

		public IWebViewVisitDelegate VisitDelegate
		{
            get => _visitDelegate;
            set => _visitDelegate = value;
		}

        public WebView(NSCoder coder) : base(coder)
        {
        }

        public void VisitLocation(NSUrl location, Enums.Action action, string restorationIdentifier = "")
        {
            CallJavascriptFunction("webView.visitLocationWithActionAndRestorationIdentifier", new object[]{location.AbsoluteString, (int)action, restorationIdentifier});
        }

        public void IssueRequestForVisit(string identifier)
        {
            CallJavascriptFunction("webView.issueRequestForVisitWithIdentifier", new object[] { identifier });
        }

        public void ChangeHistoryForVisit(string identifier)
        {
            CallJavascriptFunction("webView.changeHistoryForVisitWithIdentifier", new object[] { identifier });
        }

		public void LoadCachedSnapshotForVisit(string identifier)
		{
			CallJavascriptFunction("webView.loadCachedSnapshotForVisitWithIdentifier", new object[] { identifier });
		}

		public void LoadResponseForVisit(string identifier)
		{
			CallJavascriptFunction("webView.loadResponseForVisitWithIdentifier", new object[] { identifier });
		}

		public void CancelVisit(string identifier)
		{
			CallJavascriptFunction("webView.cancelVisitWithIdentifier", new object[] { identifier });
		}



        void CallJavascriptFunction(string functionExpression, object[] arguments, Action<NSObject> completionHandler = null)
        {
            var script = ScriptForCallingJavascriptFunction(functionExpression, arguments);

            if (script == null) return;

            EvaluateJavaScript(script, (result, error) => {

                var resultDictionary = result as NSDictionary;

                if(resultDictionary != null)
                {
                    var resultError = resultDictionary["error"]?.ToString();
                    var resultStack = resultDictionary["stack"]?.ToString();

                    if (!string.IsNullOrEmpty(resultError) && !string.IsNullOrEmpty(resultStack))
                        Console.WriteLine($"Error evaluating JavaScript function {functionExpression}: {resultError}\n{resultStack}");
                    else
                        completionHandler?.Invoke(resultDictionary["value"]);
                }
                else if(error != null)
                {
                    _delegate?.DidFailJavaScriptEvaluation(error);
                }
            });
        }

        string ScriptForCallingJavascriptFunction(string functionExpression, object[] arguments)
        {
            var encodedArguments = EncodeJavaScriptArguments(arguments);

            if (encodedArguments == null) return null;

			return "(function(result) {\n" +
	            "  try {\n" +
	            "    result.value = " + functionExpression + "(" + encodedArguments + ")\n" +
	            "  } catch (error) {\n" +
	            "    result.error = error.toString()\n" +
	            "    result.stack = error.stack\n" +
	            "  }\n" +
	            "  return result\n" +
                "})({})";
        }

        string EncodeJavaScriptArguments(object[] arguments)
        {
            Foundation.NSError error;

            var data = NSJsonSerialization.Serialize(NSArray.FromObjects(arguments), 0, out error);
            if (data == null) return null;

            NSString nsStringToReturn = new NSString(data, NSStringEncoding.UTF8);
            if (nsStringToReturn == null) return null;

            var stringToReturn = nsStringToReturn.ToString();

            return stringToReturn.Substring(1, stringToReturn.Length - 2);
        }

        public void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage scriptMessage)
        {
            var message = ScriptMessage.Parse(scriptMessage);

            if (message == null) return;

            switch(message.Name)
            {
                case Enums.ScriptMessageName.PageLoaded:
                    _pageLoadDelegate?.DidLoadPage(message.RestorarionIdentifier);
                    break;
                case Enums.ScriptMessageName.PageInvalidated:
                    _delegate?.DidInvalidatePage();
                    break;
                case Enums.ScriptMessageName.VisitProposed:
                    _delegate?.DidProposeVisit(message.Location, message.Action);
                    break;
                case Enums.ScriptMessageName.VisitStarted:
                    var hasCachedSnapshot = message.Data["hasCachedSnapshot"] as NSNumber;
                    _visitDelegate?.DidStartVisit(message.Identifier, (int)hasCachedSnapshot == 1);
                    break;
                case Enums.ScriptMessageName.VisitRequestStarted:
                    _visitDelegate?.DidStartRequestForVisit(message.Identifier);
                    break;
                case Enums.ScriptMessageName.VisitRequestCompleted:
                    _visitDelegate?.DidCompleteRequestForVisit(message.Identifier);
                    break;
                case Enums.ScriptMessageName.VisitRequestFailed:
                    _visitDelegate?.DidFailRequestForVisit(message.Identifier, (int)message.Data["statusCode"]);
                    break;
                case Enums.ScriptMessageName.VisitRequestFinished:
                    _visitDelegate?.DidFinishRequestForVisit(message.Identifier);
                    break;
                case Enums.ScriptMessageName.VisitRendered:
                    _visitDelegate?.DidRenderForVisit(message.Identifier);
                    break;
                case Enums.ScriptMessageName.VisitCompleted:
                    _visitDelegate?.DidCompleteVisit(message.Identifier, message.RestorarionIdentifier);
                    break;
                case Enums.ScriptMessageName.ErrorRaised:
                    Console.WriteLine(message.Data["error"]);
                    break;

            }
        }
    }
}
