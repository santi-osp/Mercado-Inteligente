import { Routes } from '@angular/router';
import { HomePage, SearchPage, MarketPage, ComparePage, HistoryPage, SourcesPage, SettingsPage, AnalysisPage, UiKitPage } from './feature-pages';

export const pagePaths: Record<string, string> = {
  Inicio: '',
  Buscar: 'buscar',
  'Mi mercado': 'mi-mercado',
  Comparar: 'comparar',
  Análisis: 'analisis',
  Analisis: 'analisis',
  Historial: 'historial',
  Fuentes: 'fuentes',
  Tiendas: 'fuentes',
  Ajustes: 'ajustes'
};

export const routes: Routes = [
  { path: '', component: HomePage, title: 'Inicio' },
  { path: 'buscar', component: SearchPage, title: 'Buscar' },
  { path: 'mi-mercado', component: MarketPage, title: 'Mi mercado' },
  { path: 'comparar', component: ComparePage, title: 'Comparar' },
  { path: 'analisis', component: AnalysisPage, title: 'Análisis' },
  { path: 'historial', component: HistoryPage, title: 'Historial' },
  { path: 'fuentes', component: SourcesPage, title: 'Tiendas' },
  { path: 'tiendas', component: SourcesPage, title: 'Tiendas' },
  { path: 'ajustes', component: SettingsPage, title: 'Ajustes' },
  { path: 'dev/ui-kit', component: UiKitPage, title: 'UI Kit (Dev)' },
  { path: 'dev/matching', component: ComparePage, title: 'Matching (Dev)' },
  { path: '**', redirectTo: '' }
];
