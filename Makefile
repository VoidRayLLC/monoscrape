EXECUTABLE  = monoscrape.exe
MONO_BUILD  = mcs
SDK_VERSION = 4
MY_SOURCES  = Browser.cs IScriptlet.cs JSObjectExtensions.cs Logger.cs Program.cs ResourceInterceptor.cs
SOURCES    += $(addprefix MonoScrape/,$(MY_SOURCES))
SOURCES    += ArgParse/Options.cs
SOURCES    += deveelrl/src/deveelrl/Deveel/*.cs
REFERENCES += Awesomium.Mono

# Temporary for easier compiling from within editor
MFLAGS += -debug

all: $(EXECUTABLE)

debug: MFLAGS += -debug
debug: clean all
	
clean:
	@echo Removing generated files...
	@rm -vf $(EXECUTABLE)
	@rm -vf $(EXECUTABLE).{p,m}db
	
$(EXECUTABLE): $(SOURCES)
	$(MONO_BUILD) $(SOURCES) $(addprefix -r:,$(REFERENCES)) $(addprefix -pkg:,$(PACKAGES)) -sdk:$(SDK_VERSION) $(MFLAGS) /out:$(EXECUTABLE)

.PHONY: all clean debug
