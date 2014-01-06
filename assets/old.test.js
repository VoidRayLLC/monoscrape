// I would need the OCT's, VF's, Office visits, Topography, any scanned document.  All of this stuff is in the EHR software.

(function($) {
	// --------------------------------------------------
	// print
	// --------------------------------------------------
	$.fn.print = function(results) {
		var omit = !(this.jquery && results);
		return this.each(function() {
			if(omit) console.log($("<p>").append($(this).clone().empty().attr("style", null)).html());
			else     console.log($("<p>").append($(this).clone()        .attr("style", null)).html());
		});
	};
	
	// --------------------------------------------------
	// frames
	// 
	// Returns all of the iframes as a jQuery enumerable
	// collection. For when you want to search in all 
	// iframes at the same time.
	// Usage: $.frames().find(".foo") 
	// --------------------------------------------------
	(function($) {
		$.frames = function() {
			var $result = $(), $children = $("body");
			
			while($children.length) {
				$result = $result.add($children);
				$children = $children
					.find("iframe,frame")
					.map(function() { return this.contentDocument.body; })
					.not($result)
				;
			}
			
			return $result;
		};
		// $.frames.find = function() { return $.frames().find.apply(this, arguments); };
	})(jQuery);
	
})(jQuery);

(function(NS, $) {
	// var
	 ns = {};
	
	// --------------------------------------------------
	// initialize
	// --------------------------------------------------
	ns.initialize = function() {
		if(window.injected) return;
		window.injected = true;
		Scrape.mode = Scrape.mode || "start";
		Scrape.patient = Scrape.patient || {
			id: 0
		};
		
		var screenTimeout; screenTimeout = window.setInterval(function() {
			Scrape.screenshot();
		}, 1000);
		
		try {
			ns.step();
		} catch(e) {
			ns.log(e.message);
		}
	};
	
	// --------------------------------------------------
	// error
	// --------------------------------------------------
	ns.error = function() { console.log.apply(console, arguments); };
	
	// --------------------------------------------------
	// loadPatient
	// --------------------------------------------------
	ns.loadPatient = function(patientID, patientName) {
		$.frames().find("#ScheduleIframe").attr("src", 
			"ChartViewer.aspx?PatProfID=" 
			+ patientID 
			+ "&ItemType=0&ItemID=0&FromFind=1&PatName=" 
			+ patientName
		);
	};
	
	// --------------------------------------------------
	// log
	// --------------------------------------------------
	ns.log = function() { Scrape.log.apply(Scrape, arguments); }
	
	// --------------------------------------------------
	// mode_chart
	// --------------------------------------------------
	ns.mode_chart = function() {
		ns.waitFor(5000, "patient chart",
			function() { return $.frames().find("[onclick*=Openpage]:contains(OCT)"); },
			function($this) {
				// Delete the existing chart
				$.frames().find("#ChartGrid").remove();
				// Click the All tab
				ns.log("Clicking 'OCT' tab...");
				$this.simulate("click");
				ns.step("walkChart");
			}
		);
	};
	
	// --------------------------------------------------
	// mode_gotoCharts
	// --------------------------------------------------
	ns.mode_gotoCharts = function(pageNumber) {
		// The page number should default to 1
		if(!pageNumber) pageNumber = 1;
		
		// Find the 'Chart' link
		ns.waitFor(5000, "charts link", "#MainChartLink", function($this) {
			$this.simulate("click");
			
			// Find the search button
			ns.waitFor(5000, "looking for search button", "#SearchButton", function patientList($searchButton) {
				// Click the search button
				$searchButton.simulate("click");
				
				// Gather the list of patient IDS
				ns.waitFor(10 * 1000, "patient list", "table#PatientGrid tr[ondblclick]", function nextPatient($patients) {
					// Add the patients in this table to the existing patients array
					var patients = $patients.map(function() { 
						return $(this).find("td:nth(1)").text();
					}).get();
					
					// If there are not more patients, then go back to the chart search page
					if(!patients.length) return ns.mode_gotoCharts(pageNumber+1);
					// Get the next patient
					patientID = patients.shift();
					// Load the patient into the patient chart iframe
					ns.loadPatient(patientID);
					// Find the 'OCT' tab
					ns.waitFor(9000, "'OCT' button", "[onclick*=Openpage]:contains(OCT)", function($this) {
						// Remove the chart data so that we can tell when it's loaded
						$.frames().find("#divChart").empty();
						// Click the tab
						$this.simulate("click");
						// Remove the OCT button so we can re-detect it later
						// $this.remove();
						
						ns.waitFor(10 * 1000, "chart data", "table#ChartGrid", function($chart) {
							var $rows = $chart.find("tr[click],tr[dblclick]");
							
							if(!$rows.length) {
								history.go(-2);
								// ns.waitFor(5000, "patient list from history", 
								// 	"table#PatientGrid tr[ondblclick]", nextPatient);
							}
						});
					});
				});
			});
		});
	};
	
	// --------------------------------------------------
	// mode_nextPatient
	// --------------------------------------------------
	ns.mode_nextPatient = function() {
		ns.waitFor(5000, "charts link",
			function() { return $.frames().find("#MainChartLink"); },
			function($this) {
				ns.log("Clicking MainChartLink...");
				$this.simulate("click");
				
				// Click Search button
				ns.waitFor(5000, "Search button",
					function() { return $.frames().find("#SearchButton").first(); },
					function($this) {
						ns.log("Clicking search button...");
						$this.simulate("click");
						
						ns.waitFor(10*1000, "patient record",
							function() { return $.frames().find("tr.datagridMain").first(); },
							function($this) {
								ns.patientID = $this.attr("ondblclick").match(/ShowChart\(.*?(\d+)/)[1];
								$this.simulate("dblclick");
								Scrape.mode = "chart";
								ns.step();
							},
							function() { ns.step(); }
						);
					}
				);
			}
		);
	};
	
	// --------------------------------------------------
	// mode_start
	// --------------------------------------------------
	ns.mode_start = function() {
		ns.waitFor(5000, "login form",
			function() {
				return $.frames().find("#txtUserName");
			}, 
			function() {
				ns.log("Logging in...");
				$("#txtServer").val("43");
				$("#txtUserName").val("support");
				$("#txtUserPass").val("eyeball1");
				$("#btnLogon").simulate("click");
				Scrape.mode = "gotoCharts";
			}
		);
	};
	
	// --------------------------------------------------
	// mode_walkChart
	// --------------------------------------------------
	ns.mode_walkChart = function() {
		// Wait for the chart data to load
		ns.waitFor(10*1000, "chart data",
			function() { return $.frames().find("#ChartGrid"); },
			function($this, __var, $row) {
				// Remove the header if it exists
				$this.find("tr.datagridHeader").remove();
				$row = $this.find("tr").first();
				
				if($row.length) {
					// // Delete the existing appointment table
					// $.frames().find("#ApptTbl").remove();
					// // Extract the apopintment id from the record
					// ns.appointmentID = $row.attr("onclick").match(/Opn\(\d*,.*?(\d+)/)[1];
					// // Click the top row in the ledger
					// ns.log("Clicking next row in chart...");
					// $row.simulate("click");
					
					// ns.waitFor(5000, "appointment information",
					// 	function() { return $.frames().find("#ApptTbl"); },
					// 	function($this) {
					// 		var data = "";
							
					// 		$this.find("tr").each(function() {
					// 			var $cells = $(this).find("td");
								
					// 			data += ""
					// 				+ $cells.eq(0).text().replace(/[: ]*$/, '')
					// 				+ ": "
					// 				+ $cells.eq(1).text()
					// 				+ "\n";
					// 			;
					// 		});
					// 		filename = "patient."+ns.patientID+".appointment."+ns.appointmentID;
					// 		Scrape.dumptext(filename, data);
							
					// 		// If there is no more data in the chart, then move to the next patient
					// 		if($row.next().length == 0) {
					// 			Scrape.step = "nextPatient";
					// 			Scrape.back();
					// 		} else {
					// 			// We finished this row, so delete it
					// 			$row.remove();
					// 		}
							
					// 		// Do this step again (or the next step)
					// 		ns.step();
					// 	},
					// 	5000, 
					// 	"Unable to find appointment information"
					// );
				}
				
				else {
					Scrape.mode = "nextPatient";
					Scrape.back();
					ns.step();
				}
			}
		);
	};
	
	// --------------------------------------------------
	// step
	// --------------------------------------------------
	ns.step = function(newMode) {
		// Allow an optional newMode to be passed
		if(newMode) Scrape.mode = newMode;
		// Scrape.screenshot();
		ns["mode_" + Scrape.mode]();
	};
	
	// --------------------------------------------------
	// waitFor
	// 
	// Wait for a jQuery expression (selector) to become 
	// valid. When the expression is valid, the result 
	// will be passed into successFunction. If timeout 
	// is provided, then call errorFunction upon failure. 
	// --------------------------------------------------
	ns.waitFor = function(timeout, description, expression, successFunction, errorFunction) {
		// Convert string expressions into functions
		if(typeof(expression) == "string") {
			var selector = expression;
			expression = function() { return $.frames().find(selector); };
		}
		
		var interval, startTime = new Date();
		
		ns.log("Searching for " + description + "...");
		interval = window.setInterval(function() {
			var $results = expression();
			// Found the result
			if($results.length) {
				var elapsed = Math.floor((new Date()-startTime) / 100)/10;
				ns.log("Found in " +elapsed+ " seconds.");
				successFunction.apply($results, [$results]);
				window.clearInterval(interval);
			}
			// Didn't find the result, check timeout
			else if(timeout) {
				if((new Date() - startTime) > timeout) {
					window.clearInterval(interval);
					ns.error("Unable to find: " + description);
					if(errorFunction) errorFunction();
				}
			}
		}, 200);
		
	};
	
	$(ns.initialize);
})("runner", jQuery);
