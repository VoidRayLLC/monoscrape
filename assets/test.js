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
			var $result = $(), $children = $(document);
			
			while($children.length) {
				$result = $result.add($children);
				$children = $children
					.find("iframe,frame")
					.map(function() { return this.contentDocument; })
					.not($result)
				;
			}
			
			return $result;
		};
	})(jQuery);
	
	// --------------------------------------------------
	// documents
	// --------------------------------------------------
	(function() {
		$.documents = function() {
			return $.frames().map(function() {return this.ownerDocument;});
		};
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
		
		// Click continue on session timeout warning page
		if(typeof(timerEndSession) != "undefined") clearTimeout(timerEndSession);
		if(typeof(timerWarning)    != "undefined") clearTimeout(timerWarning);
		
		var screenTimeout; screenTimeout = window.setInterval(function() {
			Scrape.screenshot();
		}, 1000);
		
		setInterval(function() {
			if(ns.waitArgs && ns.waitArgs.length) {
				var args = ns.waitArgs.pop();
				ns.waitFor.apply(this, args);
			}
		}, 333);
		
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
	// loadPatients
	// --------------------------------------------------
	ns.loadPatients = function(callback, maxPage) {
		// Make sure that callback is callable
		if(!callback) callback = function() {};
		// Load the patient IDs as text
		var text = Scrape.loadtext("patient.index").trim();
		// If there are patient IDs, then return them
		if(text.length > 1) callback(text.split(","));
		// The maximum number of pages of patients to pull
		if(!maxPage) maxPage = Number.MAX_VALUE;
		
		// --------------------------------------------------
		// Collect all the patient IDs
		// --------------------------------------------------
		
		// Find the 'Chart' link
		else ns.waitFor(5000, "charts link", "#MainChartLink", function($this) {
			ns.patients = [];
			$this.simulate("click")
			
			// Find the search button
			ns.waitFor(5000, "looking for search button", "#SearchButton", function patientList($searchButton) {
				// Click the search button
				if($searchButton) $searchButton.simulate("click");
				
				// Gather the list of patient IDS
				ns.waitFor(30 * 1000, "patient list", "table#PatientGrid tr[ondblclick]", function nextPatient($patients) {
					window.setTimeout(function() {
						// Add the patients in this table to the existing patients array
						ns.patients = ns.patients.concat($patients.map(function() { 
							return $(this).find("td:nth(1)").text();
						}).get());
						console.log("PATIENTS: " + ns.patients.length);
						// Remove the patients to eliminate false positives
						$patients.remove();
						// Get the link to the next page
						var $nextPage = $.frames().find("tr.datagridFixedHeaderPager span+a");
						// Make sure the next page is within the range
						if($nextPage.text() > maxPage) return callback(ns.patients);
						// Click the next number
						else $nextPage.simulate("click");
						
						// Next page not found, so there are no more patients. Now process the ones we gathered
						if($nextPage.length == 0) {
							// Save the list of patient IDs
							Scrape.dumptext("patient.index", ns.patients.join(","));
							// Proccess the patients
							callback(ns.patients);
						}
							
						// goto: patientList
						patientList();
					}, 500);
				});
			});
		});
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
	// mode_exportAppointments
	// --------------------------------------------------
	ns.mode_exportAppointments = function() {
		// Make a separate namespace for dealing with the found records
		function processPage() {
			// Load each patient inside itself
			$.frames().find("tr[ondblclick*=ShowChart]").eq(0).each(function() {
				var $row = $(this);
				var $td = $(this).find("td:eq(3)");
				var patientID = $row.attr("ondblclick").match(/\d+/)[0];
				
				patientID = 22170756;
				$div = $.frames().find("div[name=Agr]").empty();
				$([0,1,2,3,4,5,6,7,8,9]).each(function(i) {
					$div.append($("<div />").load("ChartViewerList.aspx?PatProfID="+patientID+"&Show=1&Color=Yellow&ItemType="+i+"&ItemId=0 table#ChartGrid"));					
				});
			});
		};
		
		// Get the charts link
		ns.waitForSimple(5000, "charts link", "#MainChartLink", function gotoChartsLink($chartsLink) {
			// Click the main charts link
			$chartsLink.simulate("click");
			
			// Wait for the search button
			ns.waitForSimple(5000, "search button", "#SearchButton", function gotoSearchButton($searchButton) {
				// Click the search button
				$searchButton.simulate("click");
				// Wait for a chart to show up, then start processing patients
				ns.waitFor(15000, "search results", "tr[ondblclick*=ShowChart]", function() {
					processPage();
				}, function() {gotoSearchButton($searchButton);} );
			}, function() {gotoChartsLink($chartsLink);} );
		});
		
		return;
		var totalPatients;
		var patientNumber;
		
		// Load the list of patient IDs from the file
		ns.loadPatients(function nextPatient(patients) {
			if(!totalPatients) totalPatients = patients.length;
			// Get the bottom patient off the list
			patientID = patients.pop();
			patientNumber = totalPatients - patients.length;
			// Log what patient we're on
			ns.log("Patient " + patientNumber + " of " + totalPatients + " (" + (patientNumber/totalPatients*100) + ")");
			
			ns.log("Loading apopintment list...");
			
			var $body = $($("#ScheduleIframe").prop("contentDocument")).find("html");
			
			$body.load("ChartViewerList.aspx?PatProfID="+patientID+"&Show=1&Color=Yellow&ItemType=3&ItemId=0 table#ChartGrid", function() {
				var count = 0, $rows = $(this).find("tr[onclick]");
				
				$rows.each(function() {
					var $row = $(this);
					var appointmentID = $row.attr("onclick").match(/,\s*(\d+)/)[1];
					
					$row.load("ChartApptDetail.aspx?ApptID=" + appointmentID, function() {
						var filename = "patient." + patientID + ".appointment." + appointmentID;
						// Write the data to the file
						Scrape.dumptext(filename, ns.tableToYAML($(this).find("tr")));
						ns.log("Wrote " + filename);
						// Done with this item, remove from list
						$row.remove();
						if(++count == $rows.length) nextPatient(patients);
					});
				});
				
				// No appointments, continue
				if(!$rows.length) nextPatient(patients);
			});
		});
	};
	
	// --------------------------------------------------
	// mode_gotoCharts
	// --------------------------------------------------
	ns.mode_gotoCharts = function(pageNumber) {
		// The page number should default to 1
		if(!pageNumber) pageNumber = 1;
		
		ns.patients = Scrape.loadtext("patient.index").split(",");
		// Special case for empty file
		if(ns.patients.length == 1) ns.patients = [];
		
		// This is where we'll go when we have all the patients
		var processPatients = function(patientID) {
			// Get the next patient
			if(!patientID) patientID = ns.patients.shift();
			// Load the patient into the patient chart iframe
			ns.loadPatient(patientID);
			// Find the 'OCT' tab
			ns.waitFor(9000, "'OCT' button", "[onclick*=Openpage]:contains(OCT)", function octButton($octButton) {
				// Remove the chart data so that we can tell when it's loaded
				$.frames().find("#divChart").empty();
				// Click the tab
				$octButton.simulate("click");
				// Remove the OCT button so we can re-detect it later
				// .remove();
				
				ns.waitFor(10 * 1000, "chart data", "table#ChartGrid", function($chart) {
					var $rows = $chart.find("tr[click],tr[dblclick]");
					
					if(!$rows.length) processPatients();
				}, function() {
					// Re-click the OCT button
					octButton($octButton);
				});
			}, function() {
				// Rerun this patient
				processPatients(patientID);
			});
		};
	};
	
	// --------------------------------------------------
	// mode_start
	// --------------------------------------------------
	ns.mode_start = function() {
		ns.waitForSimple(5000, "username field", "#USERID", function($input) {
			console.log("Entering username...");
			$input.val("schnitmann");
			console.log("Clicking submit...");
			$("input#cont").click();
		});
		// ns.waitForSimple(5000, "starting page", "#txtUserName,#MainChartLink", function() {
		// 	if($("#txtUserName").length) {
		// 		ns.log("Logging in...");
		// 		$("#txtServer").val("43");
		// 		$("#txtUserName").val("support");
		// 		$("#txtUserPass").val("eyeball1");
		// 		$("#btnLogon").simulate("click");
		// 	}
		// 	else ns.step("exportAppointments");
		// });
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
		// Unpase if pased
		ns.stopped = false;
		// Allow an optional newMode to be passed
		if(newMode) Scrape.mode = newMode;
		// Scrape.screenshot();
		ns["mode_" + Scrape.mode]();
	};
	
	// --------------------------------------------------
	// stop
	// --------------------------------------------------
	ns.stop = function() { ns.stopped = true; };
	
	// --------------------------------------------------
	// tableToYAML
	// --------------------------------------------------
	ns.tableToYAML = function(selector) {
		// Collect the data
		var data = "";
		
		$(selector).each(function() {
			var $td = $(this).find("td");
			
			data += 
				$td.eq(0).text().replace(":", "").trim()
				+ ": " 
				+ $td.eq(1).text()
				+ "\n"
			;
		});
		
		return data;
	};
	
	ns.urlPatient = function(patientID) {
		// ChartViewer.aspx?PatProfID=999999&ItemType=0&ItemID=0&FromFind=1&PatName=JimBob
		return "ChartViewer.aspx?" + $.param({
			PatProfID : patientID,
			ItemType  : 0,
			ItemID    : 0,
			FromFind  : 1,
		});
	};
	
	// --------------------------------------------------
	// waitFor
	// 
	// Calls waitForSimple, but remove the selector 
	// expression first to avoid false positives.
	// --------------------------------------------------
	ns.waitFor = function(timeout, description, expression, successFunction, errorFunction) {
		// Let the main thread handle this
		if(this == ns) ns.waitArgs = [arguments];
		
		else {
			// Remove the matched selection to prevent false positives
			$.frames().find(expression).remove();
			// Delegate to waitForSimple
			return ns.waitForSimple.apply(this, arguments);
		}
	};
	
	// --------------------------------------------------
	// waitForSimple
	// 
	// Wait for a jQuery expression (selector) to become 
	// valid. When the expression is valid, the result 
	// will be passed into successFunction. If timeout 
	// is provided, then call errorFunction upon failure. 
	// --------------------------------------------------
	ns.waitForSimple = function(timeout, description, expression, successFunction, errorFunction) {
		// Don't run if paused
		if(ns.stopped) return;
		
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
					if(errorFunction) window.setTimeout(errorFunction, 100);
				}
			}
		}, 200);
	};
	
	$(ns.initialize);
})("runner", jQuery);
