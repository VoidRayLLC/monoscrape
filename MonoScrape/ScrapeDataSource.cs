using System;
using Awesomium.Core.Data;

namespace MonoScrape {
	public class ScrapeDataSource : DataSource {
		Logger log = Logger.DefaultLogger;
		
		override protected void OnRequest(DataSourceRequest request) {
			DataSourceResponse response
			SendResponse(request, response);
		}
	}
}
