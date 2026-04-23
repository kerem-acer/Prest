SLN = Prest.slnx
BENCH_PROJ = benchmarks/Prest.Benchmarks/Prest.Benchmarks.csproj
BENCH_RUN = dotnet run --project $(BENCH_PROJ) -c Release --

.PHONY: build test coverage bench list clean restore

# Default iteration time (ms). Override: make bench ITERATION_TIME=500. Disable: make bench ITERATION_TIME=
ITERATION_TIME ?= 150

# --- Build & Test ---

build: ## Build solution
	dotnet build $(SLN) -c Release

test: ## Run all tests
	dotnet test --solution $(SLN) -c Release

coverage: ## Run tests with coverage report
	dotnet test --solution $(SLN) -c Release -- \
		--coverage --coverage-settings coverage.settings.xml \
		--coverage-output-format cobertura \
		--coverage-output coverage.cobertura.xml
	reportgenerator \
		-reports:"tests/*/bin/Release/net10.0/TestResults/coverage.cobertura.xml" \
		-targetdir:TestResults/CoverageReport \
		-reporttypes:"TextSummary"

restore: ## Restore packages
	dotnet restore $(SLN)

clean: ## Clean build outputs
	dotnet clean $(SLN) -c Release

# --- Benchmarks ---

FILTER = $(if $(F),--filter '*$(F)*',--filter '*')

# Mode flags — set to any value (usually 1) to enable
ITER_TIME_FLAG = $(if $(ITERATION_TIME),--iterationTime $(ITERATION_TIME),)
I_FLAG         = $(if $(I),-i,)
NO_FLAG        = $(if $(NO),--no-overhead,)
LOOSE_FLAG     = $(if $(LOOSE),--loose,)
MAX_ITER_FLAG  = $(if $(MAX_ITER),--maxIterationCount $(MAX_ITER),)
MIN_ITER_FLAG  = $(if $(MIN_ITER),--minIterationCount $(MIN_ITER),)
WARMUP_FLAG    = $(if $(WARMUP),--warmupCount $(WARMUP),)
PRESET_FLAG    = $(if $(PRESET),-j $(PRESET),)
MODE_FLAGS     = $(ITER_TIME_FLAG) $(I_FLAG) $(NO_FLAG) $(LOOSE_FLAG) $(MAX_ITER_FLAG) $(MIN_ITER_FLAG) $(WARMUP_FLAG) $(PRESET_FLAG)

#   make bench F=Contains                                 # full adaptive
#   make bench F=Contains P='N=256 Locale=Ascii'          # filter by params
#   make bench F=Contains PRESET=dry                      # smoke test (1 call per bench)
#   make bench F=Contains PRESET=short                    # pinned 3 iter / 3 warmup
#   make bench F=Contains I=1 NO=1 LOOSE=1 MAX_ITER=30    # dev loop (fast, loose)
#   make bench F=Contains ITERATION_TIME=500              # publishable precision
#   make bench F=Contains ARGS='--memory --threading'     # advanced: extra BDN flags
bench: ## make bench F=Contains [I=1] [NO=1] [LOOSE=1] [MAX_ITER=N] [PRESET=short/dry/medium/long] [ITERATION_TIME=ms] [ARGS=...]
	$(BENCH_RUN) $(FILTER) $(foreach p,$(P),--param:$(p)) $(MODE_FLAGS) $(ARGS)

list: ## make list F=ContainsUtf8
	$(BENCH_RUN) --list flat $(if $(F),--filter '*$(F)*')
