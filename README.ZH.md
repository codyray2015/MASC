# MASC：多应用Swagger代码生成器

[English](README.md) | [中文](README.ZH.md)

MASC是一个Swagger API客户端生成器。README文档简要介绍了两种命令行接口（CLI）方法：`request-only`和`framework-only`。这些方法用于根据模板和JSON/YAML文件生成代码。

## 使用方法

对于源代码

```bash
dotnet run -- request-only example/swagger.json out/request/ --namespac Petstore.API

dotnet run -- framework-only out/framework --namespac Petstore.API --client-name Petstore
```

在`framework/PetstoreApp.cs`中更新默认的API网关。

在代码中使用API客户端

```csharp
var apiClient = new Petstore.API.PetstoreApiClient();
var request = new Petstore.API.Request.Store.InventoryGetRequest();

var result = await apiClient.SendRequestAsync<string>(request);
```