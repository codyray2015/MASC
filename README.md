# MASC: Multiple application swagger codegen

[English](README.md) | [中文](README.ZH.md)

MASC is a swagger api client generate.  document README provides a brief overview of two command line interface (CLI) methods: `request-only` and `framework-only`. These methods are used to generate code based on templates and JSON/YAML files.


## Usage

for source code

```bash

dotnet run -- request-only exampe/swagger.json out/request/ --namespac Petstore.API

dotnet run -- framework-only out/framework --namespac Petstore.API --client-name Petstore

```

update default api geteway in `framework/PetstoreApp.cs`

use api client in code

```csharp
var apiClient = new Petstore.API.PetstoreApiClient();
var request = new Petstore.API.Request.Store.InventoryGetRequest();

var result = await apiClient.SendRequestAsync<string>(request);
```