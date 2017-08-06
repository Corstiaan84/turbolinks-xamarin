﻿namespace Turbolinks.iOS
{
    using System;
    using Foundation;
    using Turbolinks.iOS.Enums;
    using Turbolinks.iOS.Interfaces;
    using WebKit;

    public class Session : NSObject
    {
        ISessionDelegate _delegate;

        WebView _webView;
        bool _initialized;
        bool _refreshing;

        public Session(WKWebViewConfiguration webViewConfiguration)
        {
            _webView = new WebView(webViewConfiguration);

            _webView.Delegate = this;
        }

        public WKWebView WebView => _webView;


        #region Visiting

        Visit _currentVisit;
        Visit _topMostVisit;

        public Visitable TopMostVisitable => _topMostVisit?.Visitable;

        public void Visit(Visitable visitable)
        {
            VisitVisitable(visitable, Enums.Action.Advance);
        }

        void VisitVisitable(Visitable visitable, Enums.Action action)
        {
            if (visitable.VisitableURL == null)
                return;

            visitable.VisitableDelegate = this;

            Visit visit;

            if (_initialized)
            {
                visit = new JavascriptVisit(visitable, action, _webView);
                visit.RestorationIdentifier = RestorationIdentifierForVisitable(visitable);
            }
            else
            {
                visit = ColdBootVisit(visitable, action, _webView);
            }

            _currentVisit?.Cancel();
            _currentVisit = visit;

            visit.Delegate = this;
            visit.Start();
        }

        public void Reload()
        {
            if (TopMostVisitable != null)
            {
                _initialized = false;
                Visit(TopMostVisitable);
                _topMostVisit = _currentVisit;
            }
        }

        #endregion


        #region Visitable activation

        Visitable _activatedVisitable;

        void ActivateVisitable(Visitable visitable)
        {
            if (visitable != _activatedVisitable)
            {
                DeactivateVisitable(_activatedVisitable, true);

                visitable.ActivateVisitableWebView(_webView);

                _activatedVisitable = visitable;
            }
        }

        void DeactivateVisitable(Visitable visitable, bool showScreenshot = false)
        {
            if (visitable == _activatedVisitable)
            {
                if (showScreenshot)
                {
                    visitable.UpdateVisitableScreenshot();
                    visitable.ShowVisitableScreenshot();
                }

                visitable.DeactivateVisitableWebView();

                _activatedVisitable = null;
            }
        }

        #endregion


        #region Visitable restoration identifiers

        NSDictionary _visitableRestorationIdentifiers = new NSDictionary();

        string RestorationIdentifierForVisitable(Visitable visitable)
        {
            return _visitableRestorationIdentifiers.ObjectForKey(visitable.VisitableViewController);
        }

        void StoreRestorationIdentifier(string restorationIdentifier, Visitable visitable)
        {
            _visitableRestorationIdentifiers.SetValueForKey(new NSString(restorationIdentifier), visitable.VisitableViewController);
        }

        void CompleteNavigtationForCurrentVisit()
        {
            if(_currentVisit != null)
            {
                _topMostVisit = _currentVisit;
                _currentVisit.CompleteNavigation();
            }
        }


        #endregion
    }
}
