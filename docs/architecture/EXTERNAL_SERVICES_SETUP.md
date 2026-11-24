# Servicios externos — setup previsto

Este archivo define qué debe preparar el usuario, pero la ausencia temporal de credenciales NO debe detener el desarrollo técnico.

## OpenRouter

El usuario dispone de una API key con saldo para el proyecto.

Configuración requerida en la aplicación:

- Enabled
- BaseUrl (`https://openrouter.ai/api/v1` por defecto)
- ApiKey (SecretStore)
- Model
- Timeout
- MaxTokens
- funciones habilitadas
- límite de consumo opcional

El modelo debe ser seleccionable; no hardcodear uno como requisito del dominio.

Live test cuando la key esté configurada:

1. llamada mínima;
2. structured JSON válido;
3. timeout/fallo controlado;
4. invalid key controlado;
5. límite/saldo controlado.

## Google Maps Platform

No existe todavía API key. Debe crearse una cuenta/proyecto de Google Cloud por el usuario.

APIs V1 previstas:

- Geocoding API.
- Routes API.

No es obligatorio un mapa visual embebido en V1.

### Checklist humano

- [ ] crear/seleccionar proyecto Google Cloud;
- [ ] habilitar facturación si Google la exige;
- [ ] habilitar Geocoding API;
- [ ] habilitar Routes API;
- [ ] crear API key;
- [ ] restringir key a las APIs necesarias;
- [ ] configurar cuotas;
- [ ] crear alertas de presupuesto;
- [ ] guardar key mediante SecretStore de Mercado Inteligente, no en Git.

### Antes de tener key

Implementar:

- interfaces;
- settings;
- `NOT_CONFIGURED` health;
- mocks;
- parsing de coordenadas/URLs que no requiera API;
- Haversine fallback;
- tests.

### Después de key

Ejecutar live smoke tests y registrar resultados en el informe del Goal 7.

## Supermercados

No introducir credenciales privadas ni técnicas de evasión. Seguir lo validado por el diagnóstico y los límites de cada provider.
