SHELL := /bin/sh

ROOT           := $(CURDIR)
NAMESPACE      ?= jorder
OVERLAY        ?= $(ROOT)/k8s/overlays/local
BUILD_CONTEXT  ?= $(ROOT)
SERVICE        ?= identity
NO_CACHE       ?= 0
INGRESS_DOMAIN ?= jorder.localhost  # *.localhost resolves to 127.0.0.1 (RFC 6761)

# Auto-discover services that have a Dockerfile under src/JOrder.*/
SERVICES     := $(shell find "$(ROOT)/src" -maxdepth 2 -name Dockerfile | awk -F'/JOrder\\.|/Dockerfile' '$$2 { print tolower($$2) }' | sort)
NOCACHE_FLAG := $(if $(filter 1,$(NO_CACHE)),--no-cache,)

# Positional service arg: make <target> <service|all>
TARGET := $(or $(firstword $(filter $(SERVICES) all,$(wordlist 2,999,$(MAKECMDGOALS)))),$(SERVICE))
SVCS   := $(if $(filter all,$(TARGET)),$(SERVICES),$(TARGET))

# Swallow positional words so make doesn't treat them as unknown targets
.PHONY: $(SERVICES) all
$(SERVICES) all:
	@:

# ── Targets ──────────────────────────────────────────────────────────

.PHONY: list-services service-info preflight build deploy restart

list-services:
	@for s in $(SERVICES); do echo "$$s -> $$s.$(INGRESS_DOMAIN)"; done

service-info:
	@for s in $(SVCS); do \
		if ! echo " $(SERVICES) " | grep -q " $$s "; then echo "Unknown service '$$s'. Available: $(SERVICES)"; exit 1; fi; \
		echo "SERVICE=$$s  IMAGE=jorder/$${s}:local  HOST=$$s.$(INGRESS_DOMAIN)"; \
	done; \
	echo "NAMESPACE=$(NAMESPACE)  OVERLAY=$(OVERLAY)"

preflight:
	@ok=1; \
	command -v docker  >/dev/null 2>&1 && docker info >/dev/null 2>&1 \
		&& echo "[OK]   docker" \
		|| { echo "[FAIL] docker not available"; ok=0; }; \
	ctx=$$(kubectl config current-context 2>/dev/null); \
	command -v kubectl >/dev/null 2>&1 && [ -n "$$ctx" ] && kubectl cluster-info >/dev/null 2>&1 \
		&& echo "[OK]   kubectl ($$ctx)" \
		|| { echo "[FAIL] kubectl / cluster not reachable"; ok=0; }; \
	ic=$$(kubectl get ingressclass -o jsonpath='{.items[?(@.metadata.annotations.ingressclass\.kubernetes\.io/is-default-class=="true")].metadata.name}' 2>/dev/null); \
	[ -n "$$ic" ] \
		&& echo "[OK]   ingress controller ($$ic)" \
		|| { echo "[WARN] no default ingress controller found (ingress routing will not work)"; }; \
	[ "$$ok" = 1 ] && echo "Preflight passed." || { echo "Preflight failed."; exit 1; }

build:
	@for s in $(SVCS); do \
		df=$$(find "$(ROOT)/src" -maxdepth 2 -name Dockerfile | grep -i "/JOrder\.$$s/"); \
		[ -n "$$df" ] || { echo "Unknown service '$$s'. Available: $(SERVICES)"; exit 1; }; \
		echo "Building $$s..."; \
		docker build $(NOCACHE_FLAG) -f "$$df" -t "jorder/$${s}:local" "$(BUILD_CONTEXT)" || exit $$?; \
	done

deploy: preflight
	@kubectl apply -k "$(OVERLAY)"
	@for s in $(SVCS); do \
		df=$$(find "$(ROOT)/src" -maxdepth 2 -name Dockerfile | grep -i "/JOrder\.$$s/"); \
		[ -n "$$df" ] || { echo "Unknown service '$$s'. Available: $(SERVICES)"; exit 1; }; \
		echo "Deploying $$s..."; \
		docker build $(NOCACHE_FLAG) -f "$$df" -t "jorder/$${s}:local" "$(BUILD_CONTEXT)" || exit $$?; \
		kubectl -n "$(NAMESPACE)" rollout restart deployment/"$$s" || exit $$?; \
		kubectl -n "$(NAMESPACE)" rollout status  deployment/"$$s" || exit $$?; \
	done

restart: preflight
	@for s in $(SVCS); do \
		echo " $(SERVICES) " | grep -q " $$s " || { echo "Unknown service '$$s'. Available: $(SERVICES)"; exit 1; }; \
		echo "Restarting $$s..."; \
		kubectl -n "$(NAMESPACE)" rollout restart deployment/"$$s" || exit $$?; \
		kubectl -n "$(NAMESPACE)" rollout status  deployment/"$$s" || exit $$?; \
	done
