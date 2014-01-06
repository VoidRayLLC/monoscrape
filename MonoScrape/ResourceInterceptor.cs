using Awesomium.Core;
using System;

namespace MonoScrape {
	public class ResourceInterceptor : IResourceInterceptor {
		public String UserAgent = null;
		
		public ResourceInterceptor() {}
		
		public ResourceResponse OnRequest(ResourceRequest request) {
			if(UserAgent != null) 
				request.AppendExtraHeader("User-Agent", UserAgent);
			
			return null;
		}
		
		public bool OnFilterNavigation(NavigationRequest request) {
			return false;
		}
	}	
}
