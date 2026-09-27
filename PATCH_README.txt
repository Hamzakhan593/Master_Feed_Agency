MASTER FEED AGENCY - WHATSAPP SYSTEM PATCH

1. Close Visual Studio / stop the running app.
2. Open the OUTER project folder where Master_Feed_Agency.sln is located.
3. Copy everything from this patch into that folder.
4. Choose "Replace the files in the destination" when Windows asks.
5. Open Master_Feed_Agency/whatsappsettings.json and add your WhatsApp Cloud API details.
6. Run the project. The WhatsAppMessages database table is created automatically on startup.

The patch keeps the existing UI style and also preserves the Payment customer filter so only customers with baqaya > 0 appear.

See WHATSAPP_SETUP.md for the three required message templates and parameter order.
