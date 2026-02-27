# 📦 ZaapLauncher

Launcher para servidores privados de **Dofus 2.X+**.


## ⚠️ Este launcher requiere un servidor web (Apache/IIS/Nginx).
No funciona usando rutas locales tipo C:\...

Permite:

- 🔄 Descargar y actualizar el cliente automáticamente.
- 🔍 Verificar archivos con SHA256.
- 🛠 Reparar cliente dañado.
- 📰 Mostrar noticias (`news.json`).
- 🟢 Mostrar estado del servidor (Online/Offline).

---

# 🧠 Cómo funciona (explicado simple)

1. Subes tu cliente a tu VPS.
2. Generas `manifest.json`.
3. El launcher lee ese manifest.
4. Descarga solo lo que cambió.
5. Ejecuta el juego.

Nada más.

---

# 🚀 GUÍA SIMPLE (PASO A PASO)

## PASO 1 — Preparar la carpeta del cliente

En tu PC crea una carpeta con tu cliente listo.

Ejemplo:

```
C:\cliente-dofus\
```

Dentro debe estar:

```
C:\cliente-dofus\
  Dofus.exe
  data\
  app\
  META-INF\
```

⚠️ Muy importante:  
Esa carpeta debe ser EXACTAMENTE la que quieres que descarguen los jugadores.

---

## PASO 2 — Generar el manifest.json

1. Abre PowerShell.
2. Ve a la carpeta donde está el proyecto ZaapLauncher.

Ejemplo:

```
cd C:\ZaapLauncher
```

3. Ejecuta:

```
dotnet run --project ZaapLauncher/ZaapLauncher.ManifestTool -- ^
  "C:\cliente-dofus" ^
  "C:\cliente-dofus\manifest.json" ^
  "http://IPVPS/game/" ^
  "v1.0.0"
```

Explicación:

- `"C:\cliente-dofus"` → carpeta del cliente.
- `"C:\cliente-dofus\manifest.json"` → aquí se genera el manifest.
- `"http://IPVPS/game/"` → URL BASE donde estarán los archivos (sin manifest.json al final).
- `"v1.0.0"` → versión (cámbiala en cada update).

Cuando termine tendrás:

```
C:\cliente-dofus\manifest.json
```

---

## PASO 3 — Subir el cliente en una VPS Windows (XAMPP / Apache)

⚠️ MUY IMPORTANTE:

Para que el launcher funcione, tu VPS debe tener un servidor web funcionando.
Sin servidor web, no existe ninguna URL y el launcher no puede descargar nada.

---

### 1️⃣ Instalar y abrir XAMPP

- Instala XAMPP en la VPS.
- Abre el panel.
- Inicia Apache.

Debe estar en verde.

---

### 2️⃣ Ir a la carpeta pública

La carpeta pública es:

```
C:\xampp\htdocs\
```

---

### 3️⃣ Crear carpeta del juego

Crea:

```
C:\xampp\htdocs\game\
```

---

### 4️⃣ Copiar el cliente

Dentro de `game` coloca:

```
manifest.json
Dofus.exe
data\
app\
META-INF\
news.json (opcional)
```

Debe quedar así:

```
C:\xampp\htdocs\game\
  manifest.json
  Dofus.exe
  data\
  app\
  META-INF\
```

---

### 5️⃣ Comprobar que funciona

Abre el navegador y entra en:

```
http://IP_DE_TU_VPS/game/manifest.json
```

Si ves el JSON → está correcto.  
Si no abre → Apache no está iniciado o el firewall bloquea el puerto 80.

---

## PASO 4 — Generar el launcher final (LO QUE ENTREGAS A LOS JUGADORES)

⚠️ No uses `dotnet build`.

Usa:

```
dotnet publish ZaapLauncher/ZaapLauncher.App ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  /p:PublishSingleFile=false
```

Cuando termine, ve a:

```
ZaapLauncher.App\bin\Release\net8.0-windows\win-x64\publish\
```

⚠️ IMPORTANTE:

Debes enviar TODA la carpeta `publish` a los jugadores.

No solo el `.exe`.  
No borres archivos.  
No muevas cosas.  

Entrega la carpeta completa tal cual.

Lo recomendable es comprimir esa carpeta en un `.zip`.
El launcher NO necesita .NET instalado si usas --self-contained true.

---

# ⚙ CONFIGURACIÓN RECOMENDADA ANTES DE COMPILAR

1. Configurar URLs/endpoint en el código.
2. Compilar.
3. Entregar ese launcher ya configurado a todos los jugadores.


---

## Variables que debes cambiar en el código

### 1) URL por defecto del manifest

Archivo:

```
ZaapLauncher/ZaapLauncher.Core/Services/ManifestService.cs
```

Variable:

```csharp
private const string DefaultManifestUrl = "http://127.0.0.1:8080/manifest.json";
```

Cámbiala por tu URL real, por ejemplo:

```csharp
private const string DefaultManifestUrl = "http://IP_DE_TU_VPS/game/manifest.json";
```

---

### 2) Endpoint por defecto para estado Online/Offline

Archivo:
```
ZaapLauncher/ZaapLauncher.Core/Services/ServerStatusService.cs
```

Variable:

```csharp
private const string DefaultServerEndpoint = "127.0.0.1:444";
```

Cámbiala por el host:puerto de tu World/Game server, por ejemplo:

```csharp
private const string DefaultServerEndpoint = "123.45.67.89:5555";
```
---

### 3) URL base del manifest para resolver noticias (`news.json`)

Archivo:

```
ZaapLauncher/ZaapLauncher.Core/Services/NewsService.cs
```

Variable:

```csharp
private const string DefaultManifestUrl = "http://127.0.0.1:8080/manifest.json";
```

Pon la misma URL del manifest de producción para que las noticias apunten al mismo host.

⚠️ Debe coincidir con la URL usada en ManifestService.
Si no coincide, las noticias pueden no cargarse correctamente.

---

## ¿Y `settings.json` / variables de entorno?

Siguen funcionando, pero ya quedarían como opción avanzada/debug:

- `ZAAP_MANIFEST_URL` puede sobreescribir la URL del manifest.
- `ZAAP_SERVER_ENDPOINT` puede sobreescribir el endpoint online/offline.
- `%LocalAppData%\ZaapLauncher\settings.json` también puede sobreescribir manifest y firma.

Si no quieres que los jugadores toquen nada, **simplemente no uses esos overrides** y distribuye el launcher ya compilado con tus valores.

---

# 🟢 ONLINE / OFFLINE (resumen)

El estado se calcula probando conexión TCP al `host:puerto` configurado en `DefaultServerEndpoint`.

- Si responde → Online.
- Si no responde → Offline.

---

# 📰 Noticias (opcional)

Crea `news.json` y súbelo junto al manifest.

Ejemplo:

```
{
  "items": [
    {
      "id": "update-1",
      "date": "2026-03-01",
      "title": "Nueva actualización",
      "tag": "Update",
      "body": "Mejoras de rendimiento.",
      "image": "/Assets/news/images/launch.jpg",
      "link": "https://paginaservidor.com"
    }
  ]
}
```

Si no existe, el launcher sigue funcionando.

---

# 🔄 Cómo actualizar el cliente

Cada vez que cambies algo:

1. Modifica archivos en C:\cliente-dofus.
2. Sube los archivos modificados al VPS.
3. Regenera `manifest.json` con nueva versión.
4. Sube el nuevo manifest.json.

Los jugadores descargarán solo lo que cambió.

---

# 📁 Dónde se instala el cliente en el PC del jugador

El launcher instala el cliente en:

```
%LocalAppData%\ZaapLauncher\game
```

No se instala en la carpeta del launcher.

---

# ❗ Problemas comunes

No actualiza:
- Revisa que `manifest.json` abra en el navegador.

Siempre Offline:
- Revisa el puerto del servidor.
- Revisa el firewall del VPS.

Error de hash:
- Generaste el manifest desde otra carpeta distinta.

---

# 📜 Licencia

Este proyecto está bajo licencia **GNU GPL v3.0**.

Si redistribuyes versiones modificadas debes mantener la misma licencia.

---

# 👤 Créditos

Desarrollado por Shine de Inquisition para DutyFree  
https://discord.gg/8DAhv7tvxt  

Orientado a servidores privados de Dofus 2.X+