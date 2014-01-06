using Awesomium.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MonoScrape {
	public class Browser {
		protected WebView view;
		protected Logger log = Logger.DefaultLogger;
		protected List<String> postScripts = new List<String>();
		protected List<Browser> childViews = new List<Browser>();
		
		public bool IsReady {
			get {
				return view.IsDocumentReady;
			}
		}
		
		// --------------------------------------------------
		// Browser (Constructor)
		// --------------------------------------------------
		public Browser(WebSession session=null) {
			// Make a session if one wasn't provided
			if(session == null) session = WebCore.CreateWebSession(new WebPreferences());
			// Create the WebView
			view = WebCore.CreateWebView(1024, 768, session);
			// view.LoadingFrameComplete += DocumentReady;
			view.DocumentReady += DocumentReady;
			view.ConsoleMessage += ConsoleMessage;
			view.ShowCreatedWebView += OnShowCreatedWebView;
		}
		
		public Browser(WebView webView) {
			this.view = webView;
			// view.LoadingFrameComplete += DocumentReady;
			view.DocumentReady += DocumentReady;
			view.ConsoleMessage += ConsoleMessage;
			view.ShowCreatedWebView += OnShowCreatedWebView;
		}
		
		// --------------------------------------------------
		// AddPostScript
		// --------------------------------------------------
		public void AddPostScript(String script) {
			if(!postScripts.Contains(script))
				postScripts.Add(script);
		}
		
		// --------------------------------------------------
		// CreateGlobalJavascriptObject
		// --------------------------------------------------
		public JSObject CreateGlobalJavascriptObject(String name) {
			// Make sure the document is available for Javascript injection
			if(!view.IsDocumentReady) {
				// Load an empty string into the browser
				view.LoadHTML(" ");
				// Loop until the DocumentReady event fires
				WaitForLoad();
			}
			
			// Make sure a page is loaded
			return view.CreateGlobalJavascriptObject(name);
		}
		
		// --------------------------------------------------
		// ConsoleMessage
		// --------------------------------------------------
		public void ConsoleMessage(Object sender, ConsoleMessageEventArgs e) {
			log.Info("Console {0}> {1}", e.LineNumber, e.Message);
		}
		
		// --------------------------------------------------
		// DocumentReady
		// --------------------------------------------------
		protected void DocumentReady(Object sender, UrlEventArgs e) {
			// Include each post script
			foreach(String script in postScripts) Include(script);
		}
		
		// --------------------------------------------------
		// GoBack
		// --------------------------------------------------
		public void GoBack() {
			view.GoBack();
		}
		
		// --------------------------------------------------
		// Include
		// --------------------------------------------------
		public void Include(String jsFile) {
			if(!File.Exists(jsFile)) jsFile = "assets/" + jsFile;
			if(!File.Exists(jsFile)) log.Error("File not found: {0}", jsFile);
			
			// File exists
			else {
				String contents = File.ReadAllText(jsFile);
				view.ExecuteJavascript(contents);
			}
		}
		
		// --------------------------------------------------
		// LoadHTML
		// --------------------------------------------------
		public void LoadHTML(String html) {
			// Delegate to the view
			view.LoadHTML(html);
			WaitForLoad();
		}
		
		// --------------------------------------------------
		// LoadURL
		// --------------------------------------------------
		public void LoadURL(String url) {
			// Trim whitespace since url is a user-provided value
			url = url.Trim();
			// Make sure the url is property formatted
			if(!Uri.IsWellFormedUriString(url, UriKind.Absolute)) url = "http://" + url;
			if(!Uri.IsWellFormedUriString(url, UriKind.Absolute)) log.Error("Invalid url: {0}", url);
			
			else {
				// Load the url into the WebView
				log.Info("Loading URL: {0}", url);
				view.Source = new Uri(url);
				// Wait for the page to load
				WaitForLoad();
			}
		}
		
		// --------------------------------------------------
		// OnShowCreatedWebView
		// --------------------------------------------------
		public void OnShowCreatedWebView(Object sender,  ShowCreatedWebViewEventArgs e) {
			log.Info("New Web View");
			
			// Wrap the view and add to children to prevent garbage collection
			childViews.Add(new Browser(new WebView(e.NewViewInstance)) {
				log = log,
				postScripts = postScripts,
			});
		}
		
		// --------------------------------------------------
		// RunJS
		// --------------------------------------------------
		public JSValue RunJS(String js) {
			JSValue result = view.ExecuteJavascriptWithResult(js);
			
			return result;
		}
		
		// --------------------------------------------------
		// SavePNG
		// --------------------------------------------------
		public void SavePNG(String name="browser.png") {
			// log.Info("Saving screenshot");
			// Make sure name isn't null
			if(name == null) name = "browser.png";
			// Add the .png ending
			if(name.IndexOf(".png") <= 0) name += ".png";
			// Get the surface for this view
			BitmapSurface surface = (BitmapSurface) view.Surface;
			// Error if null
			if(surface == null) log.Error("Unable to obtain browser surface. Try going to another webpage");
			// Do the save
			else {
				if(surface.SaveToPNG(name)) log.Verbose("Saved to {0}", name);
				else log.Error("Unable to save image");	
			}
		}
		
		// --------------------------------------------------
		// WaitForLoad
		// --------------------------------------------------
		public void WaitForLoad() {
			// A loop to allow the view to load the page
			while(view.IsLoading || (!view.IsDocumentReady)) {
				Thread.Sleep(100);
				WebCore.Update();
			}
		}
	}
}
