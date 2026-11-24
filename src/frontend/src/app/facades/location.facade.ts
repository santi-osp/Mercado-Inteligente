import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  LocationProfile,
  LocationParseResult,
  AddressComponents,
  GeocodingResultDto,
  StoreWithDistance,
  StoreRoutePlan,
  GoogleUsageStats
} from "../models";

@Injectable({ providedIn: "root" })
export class LocationFacade {
  private readonly http = inject(HttpClient);

  readonly locationInput = signal("");
  readonly currentLatitude = signal<number | null>(null);
  readonly currentLongitude = signal<number | null>(null);
  readonly currentAddress = signal("");
  readonly currentFormattedAddress = signal("");
  readonly currentAddressComponents = signal<AddressComponents | null>(null);

  // Editable structured address fields
  readonly profileStreet = signal("");
  readonly profileNumber = signal("");
  readonly profileNeighborhood = signal("");
  readonly profilePostalCode = signal("");
  readonly profileCity = signal("");
  readonly profileState = signal("");
  readonly incompleteAddressNotice = signal("");

  readonly locationParseResult = signal<LocationParseResult | null>(null);
  readonly locationBusy = signal(false);
  readonly locationError = signal("");
  readonly locationNotice = signal("");
  readonly locationProfiles = signal<LocationProfile[]>([]);
  readonly selectedProfileId = signal<string | null>(null);
  readonly newProfileLabel = signal("");

  readonly nearbyStores = signal<StoreWithDistance[]>([]);
  readonly storesLoading = signal(false);
  readonly storesError = signal("");
  readonly selectedStoreIds = signal<string[]>([]);

  readonly routePlan = signal<StoreRoutePlan | null>(null);
  readonly routeBusy = signal(false);
  readonly routeError = signal("");
  readonly returnToOrigin = signal(true);
  readonly transportMode = signal<"Driving" | "Motorcycle" | "Bicycling" | "Walking">("Driving");
  readonly costPerKm = signal(2500);

  readonly googleUsage = signal<GoogleUsageStats | null>(null);
  readonly googleUsageLoading = signal(false);
  readonly googleUsageError = signal("");

  parseLocation() {
    const input = this.locationInput().trim();
    if (!input || this.locationBusy()) return;
    this.locationBusy.set(true);
    this.locationError.set("");
    this.locationNotice.set("");
    this.incompleteAddressNotice.set("");

    this.http.post<LocationParseResult>("/api/v1/location/parse", { input }).subscribe({
      next: res => {
        this.locationParseResult.set(res);
        if (res.status === "Success" && res.latitude !== null && res.longitude !== null) {
          this.currentLatitude.set(res.latitude);
          this.currentLongitude.set(res.longitude);
          this.currentAddress.set(res.normalizedAddress ?? input);
          this.locationNotice.set(`Ubicación establecida: ${this.currentAddress()}`);
          this.locationBusy.set(false);
          this.loadNearbyStores();
        } else if (res.status === "NeedsGeocoding" && res.normalizedAddress) {
          this.geocodeAddress(res.normalizedAddress);
        } else {
          this.locationError.set(res.errorMessage || "No se pudo resolver la ubicación introducida.");
          this.locationBusy.set(false);
        }
      },
      error: () => {
        this.locationError.set("Error de red al consultar el analizador de ubicaciones.");
        this.locationBusy.set(false);
      }
    });
  }

  geocodeAddress(address: string) {
    this.http.post<GeocodingResultDto>("/api/v1/location/geocode", { address }).subscribe({
      next: geo => {
        if (geo.providerStatus !== "LIVE_GOOGLE" && geo.providerStatus !== "CACHED_VERIFIED") {
          this.locationError.set("Ubicación no verificada. Introduce coordenadas o configura Google para resolver la dirección.");
          this.locationBusy.set(false);
          return;
        }
        this.currentLatitude.set(geo.latitude);
        this.currentLongitude.set(geo.longitude);
        this.currentAddress.set(geo.formattedAddress || address);
        this.currentFormattedAddress.set(geo.formattedAddress || address);

        if (geo.components) {
          this.currentAddressComponents.set(geo.components);
          this.profileStreet.set(geo.components.street || "");
          this.profileNumber.set(geo.components.streetNumber || "");
          this.profileNeighborhood.set(geo.components.neighborhood || "");
          this.profilePostalCode.set(geo.components.postalCode || "");
          this.profileCity.set(geo.components.city || "");
          this.profileState.set(geo.components.state || "");

          this.checkMissingRegionalComponents(geo.components);
        }

        this.locationNotice.set(`Geocodificado (${geo.providerStatus}): ${this.currentAddress()}`);
        this.locationBusy.set(false);
        this.loadNearbyStores();
      },
      error: () => {
        this.locationError.set("No se pudo geocodificar la dirección.");
        this.locationBusy.set(false);
      }
    });
  }

  private checkMissingRegionalComponents(comp: AddressComponents) {
    const missing: string[] = [];
    if (!comp.street && !this.profileStreet()) missing.push("calle");
    if (!comp.streetNumber && !this.profileNumber()) missing.push("número");
    if (!comp.neighborhood && !this.profileNeighborhood()) missing.push("barrio");
    if (!comp.postalCode && !this.profilePostalCode()) missing.push("código postal");

    if (missing.length > 0) {
      this.incompleteAddressNotice.set(
        `Necesitamos completar algunos datos para obtener precios regionales más precisos (${missing.join(", ")}).`
      );
    } else {
      this.incompleteAddressNotice.set("");
    }
  }

  loadProfiles() {
    this.http.get<LocationProfile[]>("/api/v1/location/profiles").subscribe({
      next: profiles => {
        this.locationProfiles.set(profiles);
      },
      error: () => {
        this.locationError.set("No se pudieron cargar los perfiles guardados.");
      }
    });
  }

  selectProfile(profileId: string | null) {
    if (!profileId) {
      this.selectedProfileId.set(null);
      return;
    }
    this.selectedProfileId.set(profileId);
    const profile = this.locationProfiles().find(p => p.id === profileId);
    if (!profile) return;
    if (profile.latitude !== null && profile.longitude !== null) {
      this.currentLatitude.set(profile.latitude);
      this.currentLongitude.set(profile.longitude);
      this.currentAddress.set(profile.address ?? profile.label);
      this.currentFormattedAddress.set(profile.formattedAddress ?? profile.address ?? profile.label);
      this.locationInput.set(`${profile.latitude}, ${profile.longitude}`);

      this.profileStreet.set(profile.street || "");
      this.profileNumber.set(profile.number || profile.streetNumber || "");
      this.profileNeighborhood.set(profile.neighborhood || "");
      this.profilePostalCode.set(profile.postalCode || "");
      this.profileCity.set(profile.city || "");
      this.profileState.set(profile.state || "");

      this.locationNotice.set(`Perfil seleccionado: ${profile.label}`);
      this.loadNearbyStores();
    } else if (profile.address) {
      this.locationInput.set(profile.address);
      this.parseLocation();
    }
  }

  saveProfile(label?: string) {
    if (label) {
      this.newProfileLabel.set(label);
    }
    this.saveCurrentAsProfile();
  }

  saveCurrentAsProfile() {
    const label = this.newProfileLabel().trim();
    const lat = this.currentLatitude();
    const lon = this.currentLongitude();
    if (!label) {
      this.locationError.set("Indica un nombre para el perfil (ej. Casa, Trabajo).");
      return;
    }
    if (lat === null || lon === null) {
      this.locationError.set("Debes tener una ubicación válida antes de guardar el perfil.");
      return;
    }
    this.locationBusy.set(true);

    const comp = this.currentAddressComponents();
    const payload = {
      label,
      address: this.currentAddress(),
      formattedAddress: this.currentFormattedAddress() || this.currentAddress(),
      latitude: lat,
      longitude: lon,
      country: comp?.country,
      countryCode: comp?.countryCode,
      state: this.profileState() || comp?.state,
      stateCode: comp?.stateCode,
      city: this.profileCity() || comp?.city,
      postalCode: this.profilePostalCode() || comp?.postalCode,
      neighborhood: this.profileNeighborhood() || comp?.neighborhood,
      street: this.profileStreet() || comp?.street,
      number: this.profileNumber() || comp?.streetNumber,
      streetNumber: this.profileNumber() || comp?.streetNumber
    };

    this.http.post<LocationProfile>("/api/v1/location/profiles", payload).subscribe({
      next: p => {
        this.locationProfiles.update(list => [...list, p]);
        this.newProfileLabel.set("");
        this.locationNotice.set(`Perfil "${p.label}" guardado exitosamente.`);
        this.locationBusy.set(false);
      },
      error: () => {
        this.locationError.set("No se pudo guardar el perfil.");
        this.locationBusy.set(false);
      }
    });
  }

  deleteProfile(id: string) {
    this.http.delete(`/api/v1/location/profiles/${id}`).subscribe({
      next: () => {
        this.locationProfiles.update(list => list.filter(p => p.id !== id));
        if (this.selectedProfileId() === id) {
          this.selectedProfileId.set(null);
        }
        this.locationNotice.set("Perfil eliminado.");
      },
      error: () => this.locationError.set("No se pudo eliminar el perfil.")
    });
  }

  loadNearbyStores() {
    const lat = this.currentLatitude();
    const lon = this.currentLongitude();
    if (lat === null || lon === null) {
      this.nearbyStores.set([]);
      return;
    }
    this.storesLoading.set(true);
    this.storesError.set("");
    this.http.get<StoreWithDistance[]>("/api/v1/location/stores", {
      params: { originLat: lat.toString(), originLon: lon.toString() }
    }).subscribe({
      next: stores => {
        this.nearbyStores.set(stores);
        this.storesLoading.set(false);
        if (this.selectedStoreIds().length === 0 && stores.length > 0) {
          this.selectedStoreIds.set(stores.slice(0, Math.min(3, stores.length)).map(s => s.id));
        }
      },
      error: () => {
        this.storesError.set("No se pudieron cargar las tiendas cercanas.");
        this.storesLoading.set(false);
      }
    });
  }

  toggleStoreSelection(id: string) {
    this.selectedStoreIds.update(current =>
      current.includes(id) ? current.filter(x => x !== id) : [...current, id]
    );
  }

  seedStores() {
    this.storesLoading.set(true);
    this.http.post<{ message: string; count: number }>("/api/v1/location/stores/seed", null).subscribe({
      next: () => {
        this.loadNearbyStores();
        this.loadProfiles();
      },
      error: () => {
        this.storesError.set("No se pudieron inicializar las tiendas demo.");
        this.storesLoading.set(false);
      }
    });
  }

  optimizeRoute() {
    const lat = this.currentLatitude();
    const lon = this.currentLongitude();
    const storeIds = this.selectedStoreIds().length > 0
      ? this.selectedStoreIds()
      : this.nearbyStores().slice(0, 5).map(s => s.id);
    if (lat === null || lon === null) {
      this.routeError.set("Debes configurar una ubicación de origen válida.");
      return;
    }
    if (storeIds.length === 0) {
      this.routeError.set("Selecciona al menos una tienda para optimizar la ruta.");
      return;
    }
    this.routeBusy.set(true);
    this.routeError.set("");
    this.routePlan.set(null);
    this.http.post<StoreRoutePlan>("/api/v1/routes/optimize", {
      originLatitude: lat,
      originLongitude: lon,
      storeIds,
      returnToOrigin: this.returnToOrigin(),
      transportMode: this.transportMode(),
      costPerKm: this.costPerKm()
    }).subscribe({
      next: plan => {
        this.routePlan.set(plan);
        this.routeBusy.set(false);
      },
      error: () => {
        this.routeError.set("No se pudo optimizar la ruta.");
        this.routeBusy.set(false);
      }
    });
  }

  loadGoogleUsage() {
    this.googleUsageLoading.set(true);
    this.googleUsageError.set("");
    this.http.get<GoogleUsageStats>("/api/v1/location/usage").subscribe({
      next: stats => {
        this.googleUsage.set(stats);
        this.googleUsageLoading.set(false);
      },
      error: () => {
        this.googleUsageError.set("No se pudieron consultar las estadísticas de uso de Google.");
        this.googleUsageLoading.set(false);
      }
    });
  }

  routeTypeBadgeClass(type: string): string {
    if (type === "RoadRoute" || type === "ROAD_ROUTE") return "badge-road";
    if (type === "CachedRoute" || type === "CACHED_ROUTE") return "badge-cached";
    return "badge-estimate";
  }

  routeTypeLabel(type: string): string {
    if (type === "RoadRoute" || type === "ROAD_ROUTE") return "Ruta vial (Google)";
    if (type === "CachedRoute" || type === "CACHED_ROUTE") return "Caché 24h";
    return "Estimación heurística 1.35x";
  }
}
