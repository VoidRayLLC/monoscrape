using System;

namespace MonoScrape {
	public interface IScriptlet {
		void Run(Program program, Browser browser);
	}
}
