
    if (url.includes('/api/v1/catalog/categories')) {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(FIXTURES.catalogCategories) });
    }
    if (url.includes('/api/v1/catalog/import')) {
      if (mockImportState === 'running') {
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([FIXTURES.runningCatalogJob, ...FIXTURES.catalogJobs]) });
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(FIXTURES.catalogJobs) });
    }
    if (url.includes('/api/v1/analysis/opportunities')) {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(FIXTURES.opportunities) });
    }
    if (url.includes('/api/v1/analysis/product/')) {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(FIXTURES.productAnalysis) });
    }
    if (url.includes('/api/v1/analysis/basket/')) {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(FIXTURES.basketAnalysis) });
    }
    if (url.includes('/api/v1/ai/shopping-assistant')) {
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(FIXTURES.shoppingAssistantReply) });
    }

    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'ok' }) });
  });
}
