using Awesomium.Core;
using System.IO;
using System.Threading;
using System;

namespace MonoScrape {
	public class Browser {
		protected WebView view;
		protected Logger log = Logger.DefaultLogger;
		
		// --------------------------------------------------
		// Browser (Constructor)
		// --------------------------------------------------
		public Browser(WebSession session=null) {
			// Make a session if one wasn't provided
			if(session == null) session = WebCore.CreateWebSession(new WebPreferences());
			// Create the WebView
			view = WebCore.CreateWebView(1024, 768, session);
		}
		
		// --------------------------------------------------
		// CreateGlobalJavascriptObject
		// --------------------------------------------------
		public JSObject CreateGlobalJavascriptObject(String name) {
			// Make sure the document is available for Javascript injection
			if(!view.IsDocumentReady) {
				// WebCore.Update();
				view.LoadHTML(" ");
				// WebCore.Update();
				WaitForLoad();
			}
			
			// Make sure a page is loaded
			return view.CreateGlobalJavascriptObject(name);
		}
		
		// --------------------------------------------------
		// Include
		// --------------------------------------------------
		public void Include(String jsFile) {
			if(!File.Exists(jsFile)) jsFile = "assets/" + jsFile;
			String contents = File.ReadAllText(jsFile);
			view.ExecuteJavascript(contents);
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
			if(!System.Uri.IsWellFormedUriString(url, UriKind.Absolute)) url = "http://" + url;
			// Load the url into the WebView
			log.Info("Loading URL: {0}", url);
			view.Source = new Uri(url);
			// Wait for the page to load
			WaitForLoad();
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
			log.Info("Saving screenshot");
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
				if(surface.SaveToPNG(name)) log.Info("Saved to {0}", name);
				else log.Error("Unable to save image");	
			}
		}
		
		// --------------------------------------------------
		// WaitForLoad
		// --------------------------------------------------
		public void WaitForLoad() {
			// Need to call update at least once in order to get to .IsLoading
			// WebCore.Update();
			
			// A loop to allow the view to load the page
			while(view.IsLoading || (!view.IsDocumentReady)) {
				Thread.Sleep(100);
				WebCore.Update();
			}
		}
	}
}
