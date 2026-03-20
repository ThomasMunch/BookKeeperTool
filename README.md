# BookKeeperTool

This tool is created to help App developers manage their app sales and do bookkeeping
By parsing monthly financial reports from Google Play And/Or Apples App Store Connect
into monthly total numbers in local currency used for monthly bookkeeping in Dinero or any other accounting software.
It is a command line tool.

How to use it:
#1 Download all your monthly financial reports (csv-files) from Google Play Console (Download Reports\ Financial Reports\ Revenue Reports (Sales Reports))
#2 Extract them and put them in a folder of your choice (example: C:\Reports)
#3 Name them like this: 2025-07_PlayApps.csv - [YYYY-MM]
#4 Run the tool and point it to the folder with the csv-files
#5 Use the output for your bookkeeping in Dinero or any other accounting software

The console output should be something like this:

Indtast mappe med CSV filer: C:\Reports

===== RESULTATER =====

--- 2025-01 ---
Omsætning: 24.538,77
Google fee: -3.682,91
Netto: 20.855,86
Forventet udbetaling: medio 2025-02

--- 2025-02 ---
Omsætning: 17.724,13
Google fee: -2.662,54
Netto: 15.061,59
Forventet udbetaling: medio 2025-03

--- 2025-03 ---
Omsætning: 17.536,45
Google fee: -2.635,00
Netto: 14.901,45
Forventet udbetaling: medio 2025-04
...

