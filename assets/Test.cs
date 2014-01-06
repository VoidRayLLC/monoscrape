using System;
using System.Threading;
using MonoScrape;

public class Test : IScriptlet {
	Thread screenshotThread;
	Logger log = Logger.DefaultLogger;
	bool Running = true;
	
	public void Run(Program program, Browser browser) {
		// Setup the screenshot thread
		// (screenshotThread = new Thread(() => {
		// 	while(Running) {
		// 		try {
		// 			// Save a screenshot
		// 			browser.SavePNG("browser.png");
		// 		} catch(Exception) {}
				
		// 		// Wait a second
		// 		Thread.Sleep(1000);
		// 	}
		// })).Start();
		
		browser.AddPostScript("jquery-1.10.2.min.js");
		browser.AddPostScript("jquery.simulate.js");
		browser.AddPostScript("test.js");
		browser.LoadURL("http://localhost");
	}
}
