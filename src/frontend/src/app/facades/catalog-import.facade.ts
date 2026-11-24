import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { CatalogCategory, CatalogImportJob } from "../models";

@Injectable({ providedIn: "root" })
export class CatalogImportFacade {
  private readonly http = inject(HttpClient);

  readonly selectedRetailer = signal<string>("D1");
  readonly categories = signal<CatalogCategory[]>([]);
  readonly selectedCategoryIds = signal<string[]>([]);
  readonly selectAll = signal<boolean>(false);
  readonly loadingCategories = signal<boolean>(false);
  readonly categoriesError = signal<string | null>(null);

  readonly activeJob = signal<CatalogImportJob | null>(null);
  readonly jobs = signal<CatalogImportJob[]>([]);
  readonly importStarting = signal<boolean>(false);
  readonly importNotice = signal<string | null>(null);
  readonly importError = signal<string | null>(null);

  private pollTimer: any = null;

  setRetailer(code: string) {
    this.selectedRetailer.set(code);
    this.selectedCategoryIds.set([]);
    this.selectAll.set(false);
    this.loadCategories(code);
  }

  loadCategories(code?: string) {
    const retailer = code || this.selectedRetailer();
    this.loadingCategories.set(true);
    this.categoriesError.set(null);
    this.http.get<CatalogCategory[]>(`/api/v1/catalog/categories?retailer=${encodeURIComponent(retailer)}`)
      .subscribe({
        next: (cats) => {
          this.categories.set(cats || []);
          this.loadingCategories.set(false);
        },
        error: (err) => {
          this.categoriesError.set(err.error?.message || "Error al cargar categorías");
          this.categories.set([]);
          this.loadingCategories.set(false);
        }
      });
  }

  toggleCategory(id: string) {
    const current = this.selectedCategoryIds();
    if (current.includes(id)) {
      this.selectedCategoryIds.set(current.filter(c => c !== id));
      this.selectAll.set(false);
    } else {
      this.selectedCategoryIds.set([...current, id]);
    }
  }

  toggleSelectAll() {
    const next = !this.selectAll();
    this.selectAll.set(next);
    if (next) {
      this.selectedCategoryIds.set(this.categories().map(c => c.id));
    } else {
      this.selectedCategoryIds.set([]);
    }
  }

  startImport() {
    this.importStarting.set(true);
    this.importNotice.set(null);
    this.importError.set(null);

    const body = {
      retailerCode: this.selectedRetailer(),
      categoryIds: this.selectedCategoryIds(),
      selectAll: this.selectAll()
    };

    this.http.post<CatalogImportJob>("/api/v1/catalog/import", body)
      .subscribe({
        next: (job) => {
          this.activeJob.set(job);
          this.importStarting.set(false);
          this.importNotice.set(`Importación de ${job.retailerName} iniciada.`);
          this.startPolling(job.id);
          this.loadJobs();
        },
        error: (err) => {
          this.importStarting.set(false);
          this.importError.set(err.error?.message || "No se pudo iniciar la importación.");
        }
      });
  }

  cancelJob(id: string) {
    this.http.post(`/api/v1/catalog/import/${id}/cancel`, {})
      .subscribe({
        next: () => {
          this.importNotice.set("Importación cancelada.");
          this.loadJobs();
          if (this.activeJob()?.id === id) {
            const current = this.activeJob();
            if (current) {
              this.activeJob.set({ ...current, status: "Cancelled" });
            }
          }
        },
        error: (err) => {
          this.importError.set(err.error?.message || "Error al cancelar importación.");
        }
      });
  }

  loadJobs() {
    this.http.get<CatalogImportJob[]>("/api/v1/catalog/import")
      .subscribe({
        next: (list) => {
          this.jobs.set(list || []);
          const running = list?.find(j => j.status === "Running" || j.status === "Pending");
          if (running) {
            this.activeJob.set(running);
            this.startPolling(running.id);
          }
        },
        error: () => {}
      });
  }

  private startPolling(jobId: string) {
    if (this.pollTimer) clearInterval(this.pollTimer);
    this.pollTimer = setInterval(() => {
      this.http.get<CatalogImportJob>(`/api/v1/catalog/import/${jobId}`)
        .subscribe({
          next: (job) => {
            this.activeJob.set(job);
            if (job.status !== "Running" && job.status !== "Pending") {
              clearInterval(this.pollTimer);
              this.pollTimer = null;
              this.loadJobs();
            }
          },
          error: () => {
            clearInterval(this.pollTimer);
            this.pollTimer = null;
          }
        });
    }, 2000);
  }
}