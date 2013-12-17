using System;
using System.IO;

namespace MonoScrape {
	public class Logger {
		protected static Logger defaultLogger = new Logger(output:Console.Out, error:Console.Error);
		protected TextWriter output;
		protected TextWriter error;
		
		public static Logger DefaultLogger {
			get { return defaultLogger; }
		}
		
		// --------------------------------------------------
		// Logger (Constructor)
		// --------------------------------------------------
		public Logger(TextWriter output=null, TextWriter error=null) {
			this.output = output ?? Console.Out;
			this.error = error ?? Console.Error;
		}
		
		// --------------------------------------------------
		// Error
		// --------------------------------------------------
		public void Error(String format, params object[] args) {
			// Format the message
			String message = String.Format("ERROR: " + format, args);
			// Write to STDERR
			this.error.WriteLine(message);
		}
		
		// --------------------------------------------------
		// Info
		// --------------------------------------------------
		public void Info(String format, params object[] args) {
			// Format the message
			String message = String.Format("INFO: " + format, args);
			// Write to STDOUT
			this.output.WriteLine(message);
		}
	}
}
