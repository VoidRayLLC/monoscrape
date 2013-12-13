EXECUTABLE = monoscrape.exe
MONO_BUILD = mcs
SOURCES    = **/Program.cs **/Options.cs

all: $(EXECUTABLE)

$(EXECUTABLE): $(SOURCES)
	$(MONO_BUILD) $(SOURCES) $(addprefix -pkg:,$(PACKAGES)) /out:$(EXECUTABLE)
