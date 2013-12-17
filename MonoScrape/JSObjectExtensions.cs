using Awesomium.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MonoScrape
{
	public static class JSObjectExtensions
	{
		public static JSObject After(this JSObject self, JSObject other)
		{
			// Get the next element (to insert before)
			JSObject theNext = self.Next();
			// If there is no next, then use Append
			if (theNext == null) self.Parent().Invoke("appendChild", other);
			// Otherwise, use insertBefore
			else self.Parent().Invoke("insertBefore", other, theNext);
			return self;
		}

		public static void Click(this JSObject self)
		{
			self.Each(item => { item.Trigger("click"); });
		}

		public static JSObject Closest(this JSObject self, String selector)
		{
			List<JSObject> parents = new List<JSObject>();

			// Run the selector on the whole document
			self.Document().QuerySelectorAll(selector).Each(node => {
				// Test each node and see if it contains the child
				if(node.Contains(self)) parents.Add(node);
			});

			// Return the item on the end
			return parents.LastOrDefault();
		}

		public static Boolean Contains(this JSObject self, JSObject other)
		{
			return self.Invoke("contains", other);
		}

		public static JSObject CreateElement(this JSObject self, String tagName) {
			return self.Document().Invoke("createElement", tagName);
		}

		public static JSObject Document(this JSObject self)
		{
			JSValue document = self["ownerDocument"];
			if(document.IsNull) return self;
			else return document;
		}

		public static JSObject Document(this WebView self)
		{
			return self.ExecuteJavascriptWithResult("document");
		}

		public static void Each(this JSObject self, Action<JSObject> callback)
		{
			// Don't process null JSObjects
			if (self == null) return;
			// Convert to value
			JSValue value = self;
			// Don't process null or undefined
			if (value.IsNull || value.IsUndefined) return;

			// If there is no length field, then assume each on a single result
			if (!self.HasProperty("length")) callback(self);

			else
			{
				// Go through all the items in the JSObject pseudo array
				for (int i = 0, length = self.Length(); i < length; ++i)
				{
					// Get the current item
					JSValue item = self.Eq(i);
					// If self.item(i) didn't work, then try self[i]
					if (item.IsUndefined) item = self[i.ToString()];
					// Call the callback for this item
					if (!item.IsUndefined)
						if (!item.IsNull)
							callback(item);
				}
			}
		}

		public static JSObject Eq(this JSObject self, int index)
		{
			return self[index.ToString()];
		}

		public static JSObject Find(this JSObject self, String filter)
		{
			return self.QuerySelectorAll(filter);
		}

		public static AweRect GetBoundingRect(this JSObject self)
		{
			// Get the bounding rect
			JSObject rect = self.Invoke("getBoundingClientRect");

			// Convert to awerect
			return new AweRect(
				(int) rect["left"], 
				(int) rect["top"], 
				(int) rect["width"], 
				(int) rect["height"]
				);
		}

		public static String HTML(this JSObject self)
		{
			return self["innerHTML"];
		}

		public static JSObject HTML(this JSObject self, String newHTML)
		{
			self["innerHTML"] = newHTML;
			return self;
		}

		public static int Length(this JSObject self)
		{
			if (self.HasProperty("length")) return (int)self["length"];
			return 0;
		}

		public static JSObject Next(this JSObject self)
		{
			// FIXME: Need to decide whether or not text nodes are counted
			return self["nextSibling"];
		}

		public static String OuterHTML(this JSObject self)
		{
			return self["outerHTML"];
		}

		public static JSObject Parent(this JSObject self)
		{
			return self["parentNode"];
		}

		public static JSObject Prop(this JSObject self, String property)
		{
			return self[property];
		}

		public static JSObject Prop(this WebView self, String property)
		{
			return self.ExecuteJavascriptWithResult(property);
		}

		public static JSObject QuerySelector(this JSObject self, String selector)
		{
			if (self == null) return self;
			return self.Invoke("querySelector", selector);
		}

		public static JSObject QuerySelectorAll(this JSObject self, String selector)
		{
			if (self == null) return new JSObject();
			return self.Invoke("querySelectorAll", selector);
		}

		public static String Text(this JSObject self)
		{
			return (String)self["innerText"];
		}

		public static JSObject Text(this JSObject self, String newText)
		{
			self["innerText"] = newText;
			return self;
		}

		public static void Trigger(this JSObject self, String eventName) {
			// NPE safety
			if (self == null) return;
			// Get the document
			JSObject document = self["ownerDocument"];
			// Make a javascript event object
			JSObject jsEvent = document.Invoke("createEvent", "HTMLEvents");
			// Initialize the event as a click event with bubbling and canceling
			jsEvent.Invoke("initEvent", "click", true, true);
			// Trigger the event on the object
			self.Invoke("dispatchEvent", jsEvent);
		}
		
		public static JSValue Val(this JSObject self)
		{
			return self["value"];
		}

		public static JSValue Val(this JSObject self, JSValue value)
		{
			if (self.HasProperty("value"))
			{
				// Null 
				if (null == (String)value) return self["value"];
				// Assign the value
				self["value"] = value;
				// Return the new value (another line, because the setting could have side-effects)
				return self["value"];
			}

			return value;
		}
	}
}
