import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  Provider,
  D1Health,
  D1Probe,
  ExitoHealth,
  ExitoProbe,
  ExitoRegionalProbe,
  AraHealthReport,
  AraManualOffer,
  CreateAraManualOfferRequest
} from "../models";

@Injectable({ providedIn: "root" })
export class ProviderStatusFacade {
  private readonly http = inject(HttpClient);
  private sourceRequest = 0;

  readonly providers = signal<Provider[] | null>(null);
  readonly providersLoading = signal(false);
  readonly providersError = signal("");

  readonly d1Health = signal<D1Health | null>(null);
  readonly d1HealthError = signal("");
  readonly d1Probe = signal<D1Probe | null>(null);
  readonly d1ProbeBusy = signal(false);
  readonly d1ProbeError = signal("");

  readonly exitoHealth = signal<ExitoHealth | null>(null);
  readonly exitoHealthError = signal("");
  readonly exitoProbe = signal<ExitoProbe | null>(null);
  readonly exitoProbeBusy = signal(false);
  readonly exitoProbeError = signal("");
  readonly exitoRegionalProbe = signal<ExitoRegionalProbe | null>(null);
  readonly exitoRegionalProbeBusy = signal(false);
  readonly exitoRegionalProbeError = signal("");

  readonly araHealth = signal<AraHealthReport | null>(null);
  readonly araHealthLoading = signal(false);
  readonly araHealthError = signal("");

  readonly araManualOffers = signal<AraManualOffer[]>([]);
  readonly araManualLoading = signal(false);
  readonly araManualError = signal("");
  readonly araManualBusy = signal(false);
  readonly araManualNotice = signal("");

  araProductName = "";
  araBrand = "";
  araQuantity: number | null = null;
  araUnit = "g";
  araPrice: number | null = null;
  araListPrice: number | null = null;
  araStoreName = "";
  araNotes = "";

  loadProviders() {
    if (this.providersLoading()) return;
    const requestId = ++this.sourceRequest;
    this.providersLoading.set(true);
    this.providersError.set("");
    this.d1HealthError.set("");
    this.exitoHealthError.set("");

    this.http.get<D1Health>("/api/v1/providers/d1/health").subscribe({
      next: value => { if (requestId === this.sourceRequest) this.d1Health.set(value); },
      error: () => { if (requestId === this.sourceRequest) this.d1HealthError.set("No se pudo consultar el historial local de D1."); }
    });

    this.http.get<ExitoHealth>("/api/v1/providers/exito/health").subscribe({
      next: value => { if (requestId === this.sourceRequest) this.exitoHealth.set(value); },
      error: () => { if (requestId === this.sourceRequest) this.exitoHealthError.set("No se pudo consultar el historial local de Éxito."); }
    });

    this.http.get<Provider[]>("/api/v1/providers").subscribe({
      next: providers => {
        if (requestId !== this.sourceRequest) return;
        this.providers.set(providers);
        this.providersLoading.set(false);
      },
      error: () => {
        if (requestId !== this.sourceRequest) return;
        this.providersError.set("No se pudieron cargar los estados de las fuentes. Comprueba la conexión e inténtalo de nuevo.");
        this.providersLoading.set(false);
      }
    });
  }

  probeD1() {
    if (this.d1ProbeBusy()) return;
    this.d1ProbeBusy.set(true);
    this.d1ProbeError.set("");
    this.d1Probe.set(null);
    this.http.post<D1Probe>("/api/v1/providers/d1/probe", {}).subscribe({
      next: value => { this.d1Probe.set(value); this.d1ProbeBusy.set(false); },
      error: () => { this.d1ProbeError.set("No se pudo completar el diagnóstico puntual D1."); this.d1ProbeBusy.set(false); }
    });
  }

  probeExito() {
    if (this.exitoProbeBusy()) return;
    this.exitoProbeBusy.set(true);
    this.exitoProbeError.set("");
    this.exitoProbe.set(null);
    this.http.post<ExitoProbe>("/api/v1/providers/exito/probe", {}).subscribe({
      next: value => { this.exitoProbe.set(value); this.exitoProbeBusy.set(false); },
      error: () => { this.exitoProbeError.set("No se pudo completar el diagnóstico puntual de Éxito."); this.exitoProbeBusy.set(false); }
    });
  }

  probeRegionalExito() {
    if (this.exitoRegionalProbeBusy()) return;
    this.exitoRegionalProbeBusy.set(true);
    this.exitoRegionalProbeError.set("");
    this.exitoRegionalProbe.set(null);
    this.http.post<ExitoRegionalProbe>("/api/v1/providers/exito/probe/regional", {}).subscribe({
      next: value => { this.exitoRegionalProbe.set(value); this.exitoRegionalProbeBusy.set(false); },
      error: () => { this.exitoRegionalProbeError.set("No se pudo completar el diagnóstico regional de Éxito."); this.exitoRegionalProbeBusy.set(false); }
    });
  }

  loadAraHealth() {
    if (this.araHealthLoading()) return;
    this.araHealthLoading.set(true);
    this.araHealthError.set("");
    this.http.get<AraHealthReport>("/api/v1/stores/ara/health").subscribe({
      next: report => {
        this.araHealth.set(report);
        this.araHealthLoading.set(false);
      },
      error: () => {
        this.araHealthError.set("No se pudo consultar el estado de Tiendas Ara.");
        this.araHealthLoading.set(false);
      }
    });
  }

  loadAraManualOffers() {
    if (this.araManualLoading()) return;
    this.araManualLoading.set(true);
    this.araManualError.set("");
    this.http.get<AraManualOffer[]>("/api/v1/stores/ara/manual").subscribe({
      next: offers => {
        this.araManualOffers.set(offers);
        this.araManualLoading.set(false);
      },
      error: () => {
        this.araManualError.set("No se pudieron consultar los aportes comunitarios de Ara.");
        this.araManualLoading.set(false);
      }
    });
  }

  submitAraManualOffer() {
    if (this.araManualBusy() || !this.araProductName.trim() || !this.araPrice || this.araPrice <= 0) return;
    this.araManualBusy.set(true);
    this.araManualError.set("");
    this.araManualNotice.set("");

    const payload: CreateAraManualOfferRequest = {
      productName: this.araProductName.trim(),
      brand: this.araBrand.trim() || undefined,
      quantity: this.araQuantity && this.araQuantity > 0 ? this.araQuantity : undefined,
      unit: this.araUnit || undefined,
      price: Math.round(this.araPrice),
      listPrice: this.araListPrice && this.araListPrice > 0 ? Math.round(this.araListPrice) : undefined,
      storeName: this.araStoreName.trim() || undefined,
      notes: this.araNotes.trim() || undefined
    };

    this.http.post<AraManualOffer>("/api/v1/stores/ara/manual", payload).subscribe({
      next: created => {
        this.araManualOffers.update(list => [created, ...list.filter(o => o.offerId !== created.offerId)]);
        this.araManualNotice.set(`Oferta registrada: "${created.productName}" por $${created.price}.`);
        this.araManualBusy.set(false);
        this.araProductName = "";
        this.araBrand = "";
        this.araQuantity = null;
        this.araPrice = null;
        this.araListPrice = null;
        this.araStoreName = "";
        this.araNotes = "";
        if (this.araHealth()) {
          this.loadAraHealth();
        }
      },
      error: () => {
        this.araManualError.set("No se pudo registrar la oferta manual en Ara.");
        this.araManualBusy.set(false);
      }
    });
  }

  deleteAraManualOffer(offerId: string) {
    if (this.araManualBusy() || !offerId) return;
    this.araManualBusy.set(true);
    this.araManualError.set("");
    this.araManualNotice.set("");

    this.http.delete(`/api/v1/stores/ara/manual/${encodeURIComponent(offerId)}`).subscribe({
      next: () => {
        this.araManualOffers.update(list => list.filter(o => o.offerId !== offerId));
        this.araManualNotice.set("Oferta manual eliminada correctamente.");
        this.araManualBusy.set(false);
        if (this.araHealth()) {
          this.loadAraHealth();
        }
      },
      error: () => {
        this.araManualError.set("No se pudo eliminar la oferta manual.");
        this.araManualBusy.set(false);
      }
    });
  }
}
