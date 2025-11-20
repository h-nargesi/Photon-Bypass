# Photon Bypass Plan

سیستم

	مدیریت ظرفیت
		هشدار ظرفیت سرور بر اساس مقدار مصرف
		دادن فضای خالی برای کاربر جدید

	مدیریت کاربران
		ارسال اعلان تمام شدن سروریس
		محدودیت سرعت برای کاربران پر استفاده * ماهانه؟

مدیر

	سرورها
		ویرایش مشخصات سرور
        تعیین و اجرای پردازش برای اجرا روی سرورها
            مثلا راه‌اندازی سرور و فعال یا غیرفعال کردن تانلهای مختلف
            هر مکانیز دارای یک کانتکس متغییر هست که بین اسکریپتها مشترک است

	پروفایل‌ها
		ساختن پروفایل
		تعیین هزینه

	کاربران
		ایجاد و ویرایش کاربر
		مشخصات فردی (قیمت خاص)

مشتری

	کاربران
		ثبت‌نام و ویرایش کاربر
		مشاهده وضعیت و دریافت کانفیگ
		تمدید

	نمودار مصرف

	تاریخچه


وب سایت تامین کننده و فروش vpn

این وبسایت شامل سه بخش هست:
- تبلیغات شامل یک صفحه خانه هست که خلاصله‌ای مشخصات از پلن‌ها و مزیت‌های این vpn را نشان می‌دهد.
- پروفایل کاربران که شامل صفحاتی است که برای تمدید، نمایش خلاصه وضعیت و عیره می‌شود.
    - بعضی کاربران مدیر هستند که می‌تواند اکانتهای زیر مجموعه خود را ویرایش کنند
- سرویس‌های پس زمینه که وضعیت سرورها و اکانت‌ها را کنترل می‌کند
- کاربران فقط می‌توانند به یک سرور متصل شوند

این وبسایت به منظور کنترل اکانتها به دو منبع خارجی متصل می‌شود: سرور ردیوس و روترهای میکروتیک
اتصال یه سرور ردیوس به منظور همگام سازی وضعیت کاربر در وبسایت و سرور ردیوس است

همینطور این وبسایت قابلیت ارسال ایمیل و ارسال پیام از طریق شبکه‌های اجتماعی نیز هست.

## Advertisement Pages

فقط شامل صفحه نخست می‌باشد.

### Home

Page Elements:

- *(panel)* Slogan:
    - Types of plans with price
        - Traffic without time
    - Return value in the middle of the plan
    - first 3 days is free
- *(Button)* Start => link to login page

Design Info:

[crossfit-athletes-website-template](https://nicepage.com/st/46692/crossfit-athletes-website-template)

[plan-and-book-your-flights-website-template](https://nicepage.com/st/57476/plan-and-book-your-flights-website-template)

## Profile Pages

صفحات پروفایل برای کاربران ساخته‌شده‌اند و به منظور مشاهده وضعیت استفاده و باقیمانده پلن و تمدید پلنها استفاده می‌شود. و شامل بخش‌های زیر می‌باشد:
- لاگین
- ثبت نام
- فراموشی کلمه عبور
- تعیین کلمه عبور
- داشبورد
- تغییر کلمه عبور
- پاپ آپ افزایش موجودی
- تاریخچه
- ویرایش اطلاعات
- پرسش‌های متداول
- تمدید

Design Info:

[CoreUI](https://coreui.io/product/free-angular-admin-template/#live-preview)

### Login

صفحه ورود کاربران

اجزا:

- *(auto action)* Human Test (cloudflare)
- *(button)* Google Auth => (phease 5)
- *(link)* ثبتنام => go to register page
- *(link)* Forget Password => go to forget-password page
- *(required input)* User Name
- *(required input)* Password
- *(button)* Submit

عملیات‌های جانبی:

- در صورت تلاش برای ورود با پسورد اشتباه به ادمین پیام ارسال شود

### Register

برای ثبت‌نام کاربران. بعد از ثبتنام سیستم بطور خودکار کاربر را در سرور ردیوس ثبتنام می‌کند
پسورد لاگین اکانت در وبسایت با پسورد اتصال به سرورها فرق می‌کند ولی در ابتدا یکی هستند

مشخصات:

- این صفحه در منو وجود ندارد

اجزا:

- *(auto action)* Human Test (cloudflare)
- *(required input)* User Name
- *(required input)* Email
- *(input)* Mobile
- *(input)* First Name
- *(input)* Last Name
- *(required input)* Password
- *(button)* Submit

عملیات‌های جانبی:

- ارسال پیام به کاربر در صورت ایجاد کاربر جدید

### Forgot Password

مشخصات:

- این صفحه در منو وجود ندارد

اجزا:

- *(auto action)* Human Test (cloudflare)
- *(input)* Email/Mobile
- *(button)* Submit => send code (via email/whatsapp)

### Reset Password

مشخصات:

- این صفحه در منو وجود ندارد و هیچ لینکی از این صفحه برای کاربر وجود ندارد

اجزا:

- *(hiddent input)* HashCode => from query string
- *(required input)* Password
- *(button)* Submit => to change password

### Dashbord

در صفحه داشبورد کاربر وضعیت اکانت خود را مشاهده می‌کند

اجزا:

- *(panel)* Message
    - Email/mobile are not valid
        - در همینجا کاربر می‌تواند ایمیل و یا موبایل خود را ولید کند
        - *(input)* Validation Code
        - *(button)* Send/Resend
        - *(button)* Submit
    - Account is disabled
        - در صورتی که کاربر به هر علتی غیرفعال باشد اگر قابل فعال شدن مجدد وجود داشته باشد از اینجا می‌توان آنرا فعال کرد
        - *(button)* Submit
- *(panel)* Support Panel
    - *(link)* Chat/feedback Button => outer link to WhatsApp page
- *(panel)* Account Info
    - نمایش اطلاعات کاربر
    - *(link)* Edit Account Info => Edit page
    - *(link)* Change Password => Change-Password page
    - *(link)* Change OVPN Password => Change-Password page
    - *(button)* Send Config
- *(panel)* Account State
    - Show last connection list and live time
    - *(button)* Close User Connection => close connection in mikrotik
    - Acount Balance
    - *(button)* Renewal/Change => Renewal Page
- *(panel)* Wallet Balance
    - Usege Chart will not load data on page initialize
    - Current Value
    - *(button)* Increament Button => Payment Popup => Payment Page
- *(panel)* Usege Chart
    - *(button)* Reload => reload data
- *(link)* Logout => logout => home page

### Change Password

in two mode (OpenVpn | Account)
پسورد لاگین اکانت در وبسایت با پسورد اتصال به سرورها فرق می‌کند ولی در ابتدا یکی هستند و هردو از اینجا تغییر می‌کنند

اجزا:

- *(hidden input)* Type => (OpenVpn | Account)
- *(input)* Old Password
- *(input)* New Password

### Payment Popup

اجزا:

- *(readonly)* Increment Value
- *(select)* Payment Way
- Submit => to bank page

### History

کاربر تاریخچه اقداماتی که از طرف خود، مدیر کاربر، سیستم روی اکانت انجام شده

انوع اقدامات:

- increase/decrease wallet
- increase plan
- auto sent messages
- closing connection

### Edit Info Page

- *(panel)* Email Validation
    - Show if email/mobile are not valid
    - *(input)* Validation Code
    - Send/Resend *(button)*
    - *(button)* Submit
- *(required input)* Email
- *(input)* Mobile
    - to send information via whatsapp (phease 4)
- *(input)* First Name
- *(input)* Last Name
- Submit

### FAQ

### Renewal

- Wallet Balance Panel
    - Current Value
    - Increament Button => Payment Popup
- *(select)* Users Count
- *(select)* Monthly <= month (enabled: account is disabled | account is monthly)
- *(select)* Traffic <= x25 (enabled: account is disabled | account is trafficaly)
- *(button)* Submit =>

عملیات جانبی:

- On Change to Traffic: Make sure account time is finished
- On Change to Monthly: Make sure account traffiic is finished
- Close extra connections
- اگر کاربر به مدت یک هفته از پایان پلنش می‌گذرد باید به یه سرور دیگر هدایت شود
- سیستم باید با توجه به تعداد کاربران یک سرور یک جای خالی تعیین کند
- Check Server/User Balance

## Background Services

### Servcer Capacity Management

- Server Capacity Alarm
    - Alarm Admin if users count is more than 70 percent of server capacity
    - Alarm Admin if users count is less than 100 percent of server capacity
- Restrict accounts to connect to just one server
- Get free place for new renewal

### Account Management System

- Notif user for end of service
- Deactive affter a week not renewal

## Pheases

فاز بندی پروژه

1. Web Site Core
    - Consider Unit Test
    - Cloudflare Human Test
    - Multi Account Panel
        - user-service onTargetChanged event
        - optional target in history
    - FAQ
2. MySql Performance
    - Ask ChatGPT
    - Search in Youtube
3. Fake Website
    1. Enamad
    2. samandehi.ir
    3. Bank Payment Gateway
4. Send WhatsApp Notif
    - feedback button
5. Google Auth
6. Other features
