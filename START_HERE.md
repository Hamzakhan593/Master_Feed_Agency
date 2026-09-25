# Clear Contrast v5

Is update mein sirf colours, card borders aur shadows behtar kiye gaye hain. Background warm grey-green (#dfe3d9), normal cards white, aur borders/shadows wazeh hain. Sales, payments, stock aur search ke workflows v4 jaise hi hain.

ZIP ko naye khaali folder mein extract karein. Nayi solution open karein; browser mein Ctrl+F5 se latest stylesheet load karein. Database delete na karein.

# Master Feed Agency - Easy Workflow Edition v4

## Is ZIP ko sahi tarah kholein
Visual Studio band karein. ZIP ko ek NAYE khaali folder mein extract karein; purane project folder par paste/merge na karein. Naye folder ki Master_Feed_Agency.sln open karke Rebuild Solution karein. Purana database delete na karein; zaroorat par usi connection string ko use karein.

Purane Audit, UserActivity, Recovery, Cashbook/Close aur doosre removed module files ZIP mein nahi hain. Project file ab purane bache hue module files ko compile, Razor generation aur publish se bhi exclude karti hai.



## v4 - Rozana Kaam Asaan
- Customer field mein naam, phone ya business name likhein. Neeche sahi result par click/tap karein ya arrow keys aur Enter use karein. Result mein baqaya nazar aayega.
- Product ka naam ya code search karein. Rate aur available stock dekhein. Select karne par rate bhar jayega aur cursor quantity par hoga.
- Quantity ke baad Enter dabane se next product row add hoti hai. Extra khali row ko x se hata sakte hain.
- Cash / Udhaar / Kuch Payment select karein. Total Bill, Abhi Mili Payment aur Baqi Udhaar check karke Sale Save Karein.
- Discount, notes aur owner stock override optional sections mein hain. Zaroorat par kholein.
- Payment mein customer select karein, raqam likhein, ya Poora Baqaya Wasool Karein use karein. Save karne se pehle raqam confirm karein.
- Customers aur Products ki lists typing ke saath filter hoti hain. Customer se Customers button par wapas aane par isi tab ki pichli search yaad rahegi.
- Customer list, detail aur Khata se payment seedha khulti hai; customer dobara select nahi karna parta.
- Dashboard par 4 numbers: Aaj Ki Sale, Aaj Mili Payment, Customers Se Kul Lena Hai, Kam Stock Wale Products. Neeche due/late customers aur kam stock ke direct actions hain.
- Aaj Mili Payment = aaj ki saved sales par mili raqam + aaj alag se saved customer payments. Cancelled sales/reversed payments is total mein shamil nahi. Yeh cash-drawer balance nahi hai; ismein bank/cheque payments bhi shamil ho sakti hain.
- Mahine ki sales aur detailed records Reports mein milte hain. Staff ko uski permissions ke mutabiq actions milte hain.

Search results ek dafa mein 30 tak dikhte hain; mazeed letters likh kar sahi result dhoondein. Search text likhne ke baad result select karna zaroori hai. Escape dabane se pichli confirmed selection wapas aati hai. Sirf list filters isi browser tab mein yaad rakhe jate hain; unsaved sales/payments browser storage mein save nahi hotin.

## Rozana istemal
1. Login karein. Owner Staff / Access se staff account aur unki ijazat set kar sakta hai.
2. Products / Stock mein products aur shuru ka stock add karein.
3. Customers mein customer add karein. Purana baqaya ho to sirf pehli dafa opening balance dein.
4. Nayi Sale mein Cash, Udhaar, ya Kuch Payment + Baqi Udhaar select karein. Save par bill, stock aur customer balance update honge.
5. Payment Wasool Karein mein customer aur mili hui raqam select karke receipt banayein.
6. Customers mein search karein aur Customer Khata Dekhein se sale/payment ki detail kholein.
7. Reports mein Sales Report, Udhaar Report aur Stock Report dekhein ya CSV download karein.

Ghalat payment ko permission wala user reverse kar sakta hai. Payment lagi hui sale cancel karne se pehle us payment ko reverse karein. Records permanently delete karne ke bajaye cancellation/reversal use hota hai.

## Software chalana (developer / installer)
This ZIP contains the complete cleaned source project, not a standalone Windows installer.
Requirements: .NET 9 SDK and SQL Server (Windows development can use SQL Server Express LocalDB). Restore needs NuGet access.

Open PowerShell in this extracted folder:

```powershell
dotnet restore Master_Feed_Agency.sln
dotnet build Master_Feed_Agency.sln -c Release
dotnet tool install --global dotnet-ef --version 9.0.0
```

If dotnet-ef is already installed, use an EF 9-compatible version. Set the connection string to your database (default: LocalDB / MasterFeedAgencyDb):

```powershell
$env:ConnectionStrings__DefaultConnection='Server=(localdb)\MSSQLLocalDB;Database=MasterFeedAgencyDb;Trusted_Connection=True;TrustServerCertificate=True'
dotnet ef database update --project Master_Feed_Agency
```

For the FIRST owner only, supply your own credentials in the current shell. No default login/password is shipped:

```powershell
$env:SeedOwner__Email='your-owner-email@example.com'
$env:SeedOwner__FullName='Agency Owner'
$env:SeedOwner__Password='<choose-your-own-strong-password>'
dotnet run --project Master_Feed_Agency --no-launch-profile --urls http://localhost:5080
```

Open http://localhost:5080. After the first owner is created, remove the SeedOwner environment settings before later starts. Existing accounts continue to work. Use HTTPS and the deployment examples when hosting outside your own computer.

## Existing database upgrade
Back up the existing database first; apply the migrations against a restored copy before upgrading the live installation. Do not delete the database or its migration history.

The original historical migrations are intentionally retained. The final SimplifyAgency migration aligns the current model without dropping old tables or historical data. Expenses, cashbook, owners, audit and notification pages/services/models have been physically removed from the running application; their old database records are left intact for compatibility. Customer fields omitted from the simple form also retain existing values.

## Included files
- Master_Feed_Agency/: cleaned application and deployment/backup helpers.
- Verification/: executable checks using a dedicated, disposable LocalDB database.
- TEST_RESULTS.md and VerificationResults/: validation evidence.
- REMOVED_FILES.txt: source and asset removal inventory.
- Preview/: desktop and mobile screenshots of the redesigned application.

No test database, test account credentials, build output or local secrets are included.

## v3 - Design aur labels
Warm ivory/cream backgrounds, forest-green actions, olive borders aur darker text. Laptop ki Night Light setting ki zaroorat ke baghair warmer palette software ke andar hi apply hoti hai. Actual monitor aur Night Light setting ke mutabiq rang thore mukhtalif nazar aa sakte hain.

Dashboard cards mein label, raqam aur explanation ab ek doosre par overlap nahi karte. Aaj Ki Due Payment ka matlab aaj payment ki tareekh wale unpaid bills hain; Late Payment Ka Baqaya ka matlab guzar chuki tareekh wale unpaid bills hain. Kam Stock card product count dikhata hai.

Customer/product/staff edit buttons, stock setup instructions aur form headings ko clearer wording di gayi hai. Core sale/payment/stock calculations aur BuildFix v2 protection retained hain.

Naye folder mein extract karke solution open karein. Browser purana look dikhaye to Ctrl+F5 karein.
