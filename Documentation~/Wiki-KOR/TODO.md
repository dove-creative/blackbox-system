- 비동기 Exert 시 명시적으로 연결하기
``` CSharp
await _bb.Exert(worker, "LoadAsync")
.RunAsync(() => worker.LoadAsync());
```
