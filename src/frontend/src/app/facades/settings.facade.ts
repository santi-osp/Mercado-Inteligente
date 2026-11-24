import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  Settings,
  SecretEntry,
  GoogleUsageStats,
  AiStatusResponse,
  AiConnectionTestResult,
  SystemStatusDto,
  BackupFileDto,
  BackupVerificationResultDto,
  DatabaseStatsDto,
  DatabaseActionResultDto
} from '../models';

@Injectable({ providedIn: 'root' })
export class SettingsFacade {
  private readonly http = inject(HttpClient);

  readonly settings = signal<Settings | null>(null);
  readonly settingsLoading = signal(false);
  readonly settingsBusy = signal(false);
  readonly settingsError = signal('');
  readonly settingsNotice = signal('');

  readonly secrets = signal<SecretEntry[]>([
    { type: 'google', label: 'Google Maps', configured: null, loading: false, busy: false, error: '', notice: '', draft: '' },
    { type: 'omnirouter', label: 'OmniRouter (IA)', configured: null, loading: false, busy: false, error: '', notice: '', draft: '' }
  ]);

  readonly googleUsage = signal<GoogleUsageStats | null>(null);
  readonly googleUsageLoading = signal(false);
  readonly googleUsageError = signal('');

  readonly aiStatus = signal<AiStatusResponse | null>(null);
  readonly aiLoading = signal(false);
  readonly aiError = signal('');
  readonly aiNotice = signal('');
  readonly aiConnectionTest = signal<AiConnectionTestResult | null>(null);
  readonly aiConnectionTestBusy = signal(false);
  readonly aiCacheClearBusy = signal(false);

  readonly systemStatus = signal<SystemStatusDto | null>(null);
  readonly systemStatusLoading = signal(false);
  readonly systemStatusError = signal('');

  // Backups
  readonly backupsList = signal<BackupFileDto[]>([]);
  readonly backupsLoading = signal(false);
  readonly backupsError = signal('');
  readonly backupBusy = signal(false);
  readonly backupOperationStatus = signal('');
  readonly backupOperationError = signal('');
  readonly verifications = signal<Record<string, BackupVerificationResultDto>>({});

  // Database Stats & Tools
  readonly dbStats = signal<DatabaseStatsDto | null>(null);
  readonly dbStatsLoading = signal(false);
  readonly dbStatsError = signal('');
  readonly lastStatsRefresh = signal<Date | null>(null);
  readonly optimizeBusy = signal(false);
  readonly optimizeNotice = signal('');

  // Danger Zone
  readonly dangerBusy = signal(false);
  readonly dangerNotice = signal('');
  readonly dangerError = signal('');

  loadSettings() {
    this.settingsLoading.set(true);
    this.http.get<Settings>('/api/v1/settings').subscribe({
      next: s => {
        this.settings.set(s);
        this.settingsLoading.set(false);
      },
      error: () => {
        this.settingsError.set('No se pudieron cargar los ajustes.');
        this.settingsLoading.set(false);
      }
    });
  }

  saveSettings() {
    const value = this.settings();
    if (!value || this.settingsBusy()) return;
    this.settingsBusy.set(true);
    this.settingsNotice.set('');
    this.settingsError.set('');
    this.http.put<Settings>('/api/v1/settings', value).subscribe({
      next: updated => {
        this.settings.set(updated);
        this.settingsBusy.set(false);
        this.settingsNotice.set('Ajustes guardados.');
      },
      error: () => {
        this.settingsBusy.set(false);
        this.settingsError.set('No se guardaron los ajustes.');
      }
    });
  }

  private updateSecret(secret: SecretEntry, changes: Partial<SecretEntry>) {
    this.secrets.update(current =>
      current.map(entry => (entry.type === secret.type ? { ...entry, ...changes } : entry))
    );
  }

  setSecretDraft(secret: SecretEntry, value: string) {
    this.updateSecret(secret, { draft: value });
  }

  loadSecret(secret: SecretEntry) {
    if (secret.loading || secret.busy) return;
    this.updateSecret(secret, { loading: true, error: '' });
    this.http.get<{ configured: boolean }>(`/api/v1/secrets/${encodeURIComponent(secret.type)}`).subscribe({
      next: status => this.updateSecret(secret, { configured: status.configured, loading: false }),
      error: () => this.updateSecret(secret, { error: 'No se pudo consultar el estado de la clave.', loading: false })
    });
  }

  saveSecret(secret: SecretEntry) {
    if (secret.loading || secret.busy || !secret.draft.trim()) return;
    this.updateSecret(secret, { busy: true, error: '', notice: '' });
    this.http.post<{ configured: boolean }>(`/api/v1/secrets/${encodeURIComponent(secret.type)}`, { value: secret.draft }).subscribe({
      next: () => this.updateSecret(secret, { configured: true, draft: '', busy: false, notice: 'Clave guardada en este equipo.' }),
      error: () => this.updateSecret(secret, { error: 'No se pudo guardar la clave. Comprueba el formato e inténtalo de nuevo.', busy: false })
    });
  }

  deleteSecret(secret: SecretEntry) {
    if (secret.loading || secret.busy || secret.configured !== true) return;
    this.updateSecret(secret, { busy: true, error: '', notice: '' });
    this.http.delete<void>(`/api/v1/secrets/${encodeURIComponent(secret.type)}`).subscribe({
      next: () => this.updateSecret(secret, { configured: false, draft: '', busy: false, notice: 'Clave eliminada de este equipo.' }),
      error: () => this.updateSecret(secret, { error: 'No se pudo eliminar la clave. Inténtalo de nuevo.', busy: false })
    });
  }

  loadGoogleUsage() {
    this.googleUsageLoading.set(true);
    this.googleUsageError.set('');
    this.http.get<GoogleUsageStats>('/api/v1/location/usage').subscribe({
      next: stats => {
        this.googleUsage.set(stats);
        this.googleUsageLoading.set(false);
      },
      error: () => {
        this.googleUsageError.set('No se pudo consultar el consumo de Google Maps.');
        this.googleUsageLoading.set(false);
      }
    });
  }

  loadAiStatus() {
    if (this.aiLoading()) return;
    this.aiLoading.set(true);
    this.aiError.set('');
    this.http.get<AiStatusResponse>('/api/v1/ai/status').subscribe({
      next: status => {
        this.aiStatus.set(status);
        this.aiLoading.set(false);
      },
      error: () => {
        this.aiError.set('No se pudo consultar el estado del servicio de IA.');
        this.aiLoading.set(false);
      }
    });
  }

  saveAiSettings() {
    const current = this.aiStatus();
    if (!current || this.aiLoading()) return;
    this.aiLoading.set(true);
    this.aiNotice.set('');
    this.aiError.set('');

    const payload = {
      enabled: current.enabled,
      baseUrl: current.baseUrl,
      model: current.model,
      timeoutSeconds: current.timeoutSeconds,
      maxTokens: current.maxTokens,
      temperature: current.temperature,
      normalizationEnabled: current.features.normalization,
      matchingEnabled: current.features.matching,
      substituteEnabled: current.features.substitutes,
      explanationEnabled: current.features.explanation,
      monthlyBudgetUsd: current.monthlyBudgetUsd,
      warningBudgetUsd: current.warningBudgetUsd ?? null,
      hardBudgetLimit: current.hardBudgetLimit ?? false
    };

    this.http.put('/api/v1/ai/settings', payload).subscribe({
      next: () => {
        this.aiLoading.set(false);
        this.aiNotice.set('Configuración de IA actualizada.');
      },
      error: () => {
        this.aiError.set('No se pudo guardar la configuración de IA.');
        this.aiLoading.set(false);
      }
    });
  }

  testAiConnection() {
    if (this.aiConnectionTestBusy()) return;
    this.aiConnectionTestBusy.set(true);
    this.aiConnectionTest.set(null);
    this.http.post<AiConnectionTestResult>('/api/v1/ai/test-connection', {}).subscribe({
      next: res => {
        this.aiConnectionTest.set(res);
        this.aiConnectionTestBusy.set(false);
      },
      error: () => {
        this.aiConnectionTest.set({
          success: false,
          statusCode: 500,
          message: 'Error al conectar con el endpoint de prueba de IA.',
          latencyMs: 0,
          modelTested: null
        });
        this.aiConnectionTestBusy.set(false);
      }
    });
  }

  clearAiCache() {
    if (this.aiCacheClearBusy()) return;
    this.aiCacheClearBusy.set(true);
    this.http.post<{ cleared: boolean }>('/api/v1/ai/cache/clear', {}).subscribe({
      next: () => {
        this.aiCacheClearBusy.set(false);
        this.aiNotice.set('Caché local de respuestas de IA vaciada con éxito.');
        this.loadAiStatus();
      },
      error: () => {
        this.aiCacheClearBusy.set(false);
        this.aiError.set('No se pudo vaciar la caché de IA.');
      }
    });
  }

  loadSystemStatus() {
    this.systemStatusLoading.set(true);
    this.systemStatusError.set('');
    this.http.get<SystemStatusDto>('/api/v1/system/status').subscribe({
      next: status => {
        this.systemStatus.set(status);
        this.systemStatusLoading.set(false);
      },
      error: () => {
        this.systemStatusError.set('No se pudo consultar el estado de observabilidad del sistema.');
        this.systemStatusLoading.set(false);
      }
    });
  }

  // Backups CRUD
  loadBackups() {
    this.backupsLoading.set(true);
    this.backupsError.set('');
    this.http.get<BackupFileDto[]>('/api/v1/backups').subscribe({
      next: list => {
        this.backupsList.set(list);
        this.backupsLoading.set(false);
      },
      error: () => {
        this.backupsError.set('No se pudieron consultar las copias de seguridad.');
        this.backupsLoading.set(false);
      }
    });
  }

  createBackup(label?: string) {
    this.backupBusy.set(true);
    this.backupOperationStatus.set('Creando copia de seguridad...');
    this.backupOperationError.set('');
    this.http.post<{ file: string; label?: string }>('/api/v1/backups', { label: label?.trim() || null }).subscribe({
      next: res => {
        this.backupBusy.set(false);
        this.backupOperationStatus.set(`Copia creada: ${res.file}`);
        this.loadBackups();
        this.loadDatabaseStats();
      },
      error: () => {
        this.backupBusy.set(false);
        this.backupOperationError.set('Error al crear la copia de seguridad.');
      }
    });
  }

  createArchive(label?: string) {
    this.backupBusy.set(true);
    this.backupOperationStatus.set('Creando archivo completo con evidencia RAW...');
    this.backupOperationError.set('');
    this.http.post<{ file: string; label?: string }>('/api/v1/backups/archive', { label: label?.trim() || null }).subscribe({
      next: res => {
        this.backupBusy.set(false);
        this.backupOperationStatus.set(`Archivo completo creado: ${res.file}`);
        this.loadBackups();
        this.loadDatabaseStats();
      },
      error: () => {
        this.backupBusy.set(false);
        this.backupOperationError.set('Error al crear el archivo completo de copia.');
      }
    });
  }

  verifyBackup(name: string) {
    this.http.post<BackupVerificationResultDto>(`/api/v1/backups/${encodeURIComponent(name)}/verify`, {}).subscribe({
      next: res => {
        this.verifications.update(v => ({ ...v, [name]: res }));
      },
      error: () => {
        this.backupOperationError.set(`Error al verificar la copia ${name}.`);
      }
    });
  }

  restoreBackup(name: string, onComplete?: (success: boolean) => void) {
    this.backupBusy.set(true);
    this.backupOperationStatus.set(`Restaurando copia ${name}... Se creará un respaldo preventivo.`);
    this.backupOperationError.set('');
    this.http.post<{ message: string; backup: string }>(`/api/v1/backups/${encodeURIComponent(name)}/restore`, {}).subscribe({
      next: res => {
        this.backupBusy.set(false);
        this.backupOperationStatus.set(res.message || 'Copia restaurada exitosamente.');
        this.loadBackups();
        this.loadDatabaseStats();
        if (onComplete) onComplete(true);
      },
      error: err => {
        this.backupBusy.set(false);
        const msg = err.error?.message || 'No se pudo restaurar la copia de seguridad.';
        this.backupOperationError.set(msg);
        if (onComplete) onComplete(false);
      }
    });
  }

  deleteBackup(name: string) {
    this.http.delete(`/api/v1/backups/${encodeURIComponent(name)}`).subscribe({
      next: () => {
        this.backupOperationStatus.set(`Copia ${name} eliminada.`);
        this.loadBackups();
        this.loadDatabaseStats();
      },
      error: () => {
        this.backupOperationError.set(`No se pudo eliminar la copia ${name}.`);
      }
    });
  }

  openBackupsFolder() {
    this.http.post<{ path: string }>('/api/v1/backups/open-folder', {}).subscribe({
      next: res => {
        this.backupOperationStatus.set(`Carpeta abierta en el explorador: ${res.path}`);
      },
      error: () => {
        this.backupOperationError.set('No se pudo abrir la carpeta de copias.');
      }
    });
  }

  // Database Stats
  loadDatabaseStats() {
    this.dbStatsLoading.set(true);
    this.dbStatsError.set('');
    this.http.get<DatabaseStatsDto>('/api/v1/database/stats').subscribe({
      next: stats => {
        this.dbStats.set(stats);
        this.dbStatsLoading.set(false);
        this.lastStatsRefresh.set(new Date());
      },
      error: () => {
        this.dbStatsError.set('No se pudieron cargar las estadísticas de la base de datos.');
        this.dbStatsLoading.set(false);
      }
    });
  }

  optimizeDatabase() {
    this.optimizeBusy.set(true);
    this.optimizeNotice.set('');
    this.http.post<DatabaseActionResultDto>('/api/v1/database/optimize', {}).subscribe({
      next: res => {
        this.optimizeBusy.set(false);
        this.optimizeNotice.set(res.message);
        this.loadDatabaseStats();
      },
      error: () => {
        this.optimizeBusy.set(false);
        this.optimizeNotice.set('Error al optimizar la base de datos.');
      }
    });
  }

  // Danger Zone
  clearCatalog(confirmation: string, onComplete?: (success: boolean) => void) {
    this.dangerBusy.set(true);
    this.dangerError.set('');
    this.dangerNotice.set('');
    this.http.post<DatabaseActionResultDto>('/api/v1/database/clear-catalog', { confirmation }).subscribe({
      next: res => {
        this.dangerBusy.set(false);
        this.dangerNotice.set(res.message);
        this.loadDatabaseStats();
        if (onComplete) onComplete(true);
      },
      error: err => {
        this.dangerBusy.set(false);
        this.dangerError.set(err.error?.message || 'Error al borrar el catálogo.');
        if (onComplete) onComplete(false);
      }
    });
  }

  clearHistory(confirmation: string, onComplete?: (success: boolean) => void) {
    this.dangerBusy.set(true);
    this.dangerError.set('');
    this.dangerNotice.set('');
    this.http.post<DatabaseActionResultDto>('/api/v1/database/clear-history', { confirmation }).subscribe({
      next: res => {
        this.dangerBusy.set(false);
        this.dangerNotice.set(res.message);
        this.loadDatabaseStats();
        if (onComplete) onComplete(true);
      },
      error: err => {
        this.dangerBusy.set(false);
        this.dangerError.set(err.error?.message || 'Error al borrar el historial de precios.');
        if (onComplete) onComplete(false);
      }
    });
  }

  clearCache(onComplete?: (success: boolean) => void) {
    this.dangerBusy.set(true);
    this.dangerError.set('');
    this.dangerNotice.set('');
    this.http.post<DatabaseActionResultDto>('/api/v1/database/clear-cache', {}).subscribe({
      next: res => {
        this.dangerBusy.set(false);
        this.dangerNotice.set(res.message);
        this.loadDatabaseStats();
        if (onComplete) onComplete(true);
      },
      error: err => {
        this.dangerBusy.set(false);
        this.dangerError.set(err.error?.message || 'Error al limpiar la caché.');
        if (onComplete) onComplete(false);
      }
    });
  }

  clearBaskets(confirmation: string, onComplete?: (success: boolean) => void) {
    this.dangerBusy.set(true);
    this.dangerError.set('');
    this.dangerNotice.set('');
    this.http.post<DatabaseActionResultDto>('/api/v1/database/clear-baskets', { confirmation }).subscribe({
      next: res => {
        this.dangerBusy.set(false);
        this.dangerNotice.set(res.message);
        this.loadDatabaseStats();
        if (onComplete) onComplete(true);
      },
      error: err => {
        this.dangerBusy.set(false);
        this.dangerError.set(err.error?.message || 'Error al eliminar las listas.');
        if (onComplete) onComplete(false);
      }
    });
  }

  resetDatabase(confirmation: string, onComplete?: (success: boolean) => void) {
    this.dangerBusy.set(true);
    this.dangerError.set('');
    this.dangerNotice.set('');
    this.http.post<DatabaseActionResultDto>('/api/v1/database/reset', { confirmation }).subscribe({
      next: res => {
        this.dangerBusy.set(false);
        this.dangerNotice.set(res.message);
        this.loadDatabaseStats();
        if (onComplete) onComplete(true);
      },
      error: err => {
        this.dangerBusy.set(false);
        this.dangerError.set(err.error?.message || 'Error al restablecer la base de datos.');
        if (onComplete) onComplete(false);
      }
    });
  }

  formatBytes(bytes: number | null | undefined): string {
    if (bytes === null || bytes === undefined || isNaN(bytes)) return '—';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }
}
