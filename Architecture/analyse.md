# Photon Bypass Analyse

بعضی نکات:

- محدودیت api call rate limit در یک بازه زمانی
- کش شدن اطلاعات کاربر در فرانت
- محدودیت برای ارسال ایمیل در یک زمان مشخص
- برای ارسال کانفیگ open-vpn در صورت عدم وجود باید گواهی کلاینت ساخته شود

## API list

### Basics

- **prices**:

    GET `api/basics/prices`\
    response:
    ```json
    [{
        title: string;
        caption: string;
        description: string[];
    }]
    ```

### Authentication

- **token**:

    POST `api/auth/token`\
    request:
    ```json
    {
        username: string; 
        password: string;
    }
    ```

- **logout**:

    GET `api/auth/logout`

- **reset-pass**:

    POST `api/auth/reset-pass`\
    request:
    ```json
    {
        emailMobile: string;
    }
    ```

- **register**:

    POST `api/auth/register`\
    request:
    ```json
    {
        username: string;
        email: string;
        mobile: string;
        firstname: string;
        lastname: string;
        password: string;
    }
    ```

### Account

- **get-user**:

    GET `api/account/get-user`
    response:
    ```json
    {
        username: string;
        fullname: string;
        email: string;
        picture?: string;
        balance: number;
        targetArea?: {
            [username: string]: {
                username: string;
                fullname: string;
                email: string;
            }
        }
    }
    ```

- **change-pass**:

    POST `api/account/change-pass`\
    request:
    ```json
    {
        token: string;
        password: string;
    }
    ```

- **full-info**:

    GET `api/account/full-info`\
    request:
    ```json
    {
        target?: string;
    }
    ```
    response:
    ```json
    {
        username: string;
        email: string;
        emailValid: boolean;
        mobile: string;
        mobileValid: boolean;
        firstname: string;
        lastname: string;
    }
    ```

- **edit-user**:

    POST `api/account/edit-user`\
    request:
    ```json
    {
        email: string;
        mobile: string;
        firstname: string;
        lastname: string;
    }
    ```

- **history**:

    GET `api/account/history`\
    request:
    ```json
    {
        target?: string;
        from?: number;
        to?: number;
    }
    ```
    response:
    ```json
    [{
        id: number;
        target: string;
        eventTime: number;
        eventTimeTitle: string;
        title: string;
        color: string;
        value?: any;
        unit?: string;
        description?: string;
    }]
    ```

### VPN

- **change-ovpn**:

    POST `api/vpn/change-ovpn`\
    request:
    ```json
    {
        token: string;
        password: string;
        target?: string;
    }
    ```

- **send-cert-email**:

    GET `api/vpn/send-cert-email`\
    request:
    ```json
    {
        target?: string;
    }
    ```

- **traffic-data**:

    GET `api/vpn/traffic-data`\
    request:
    ```json
    {
        target?: string;
    }
    ```
    response:
    ```json
    {
        title: string;
        collections: TrafficData[];
        labels: string[];
    }
    ```

### Connection

- **current-con-state**:

    GET `api/connection/current-con-state`\
    request:
    ```json
    {
        target?: string;
    }
    ```
    response: `number[]`

- **close-con**:

    POST `api/connection/close-con`\
    request:
    ```json
    {
        index: number;
        target?: string;
    }
    ```

### Plan

- **plan-state**:

    GET `api/plan/plan-state`\
    request:
    ```json
    {
        target?: string;
    }
    ```
    response:
    ```json
    {
        type: PlanType;
        remainsTitle: string;
        remainsPercent: number;
        simultaneousUserCount: number;
    }
    ```

- **plan-info**:

    GET `api/plan/plan-info`\
    request:
    ```json
    {
        target?: string;
    }
    ```
    response:
    ```json
    {
        target: string;
        type: PlanType;
        value: number;
        simultaneousUserCount: number;
    }
    ```

- **estimate**:

    POST `api/plan/estimate`\
    request:
    ```json
    {
        target: string;
        type: PlanType;
        value: number;
        simultaneousUserCount: number;
    }
    ```
    response: `number`

- **renewal**:

    POST `api/plan/renewal`\
    request:
    ```json
    {
        target: string;
        type: PlanType;
        value: number;
        simultaneousUserCount: number;
    }
    ```
    response:
    ```json
    {
        currentPrice: number;
        moneyNeeds: number;
    }
    ```

## Out Source Calls

منابعی که وبسایت باید به آن متصل شود شامل پنج بخش هست:
- دیتابیس محلی *local db*
- سرور ردیوس
    - خواند داده‌ها از طریق اتصال مستقیم به دیتابیس ردیوس انجام می‌شود *rad db*
    - ویرایش از طریق اتصال به apiها انجام ‌می‌شود *rad api*
- روترهای میکروتیک *mikrotik ssh*
- واتساپ *whatsapp api*
- سرور ایمیل *email server*

### Base On Pages

بررسی خواند/نوشتن روی ردیوس بر اساس صفحات

- Login
    - (local db) بررسی کاربر و پسورد
    - (rad db) بررسی اکانت در سرور ردیوس در صورت عدم وجود کاربر در وبسایت
    - (local db) در صورت عدم وجود اکانت در دیتابیس محلی، اکانت باید از ردیوس کپی شود
    - (whatsapp api) هشدار پسورد اشتباه با ادمین
- Register
    - (local db) ذخیره کاربر
    - (rad api) ذخیره کاربر
- Dashboard
    - Validation
        - (email server) email verification
        - (whatsapp api) mobile verification
    - Name/Email Panel
        - (local db) خواندن اطلاعات اکانت
    - Account State > Show User's connection
        - (rad db) لیست کانکشنهای کاربر
        - (mikrotik ssh) مقایسه با اتصالات فعال روی روترها
    - Account State > Close User Connection
        - (mikrotik ssh) بستن کانکشن روی روتر
        - (rad api) بستن کانکشن روی سرور ردیوس
    - Account State Panel > Show Acount Remaining
        - (rad db) نمایش وضعیت اکانت
    - Wallet Balance Panel
        - (local db) موجودی حساب در دیتابیس محلی ذخیره ‌می‌شوند
    - Usege Chart > Reload
        - (rad api) بررسی ترافیکهای جدید - این استثنا برای خواند از api استفاده می‌کند
        - (local db) لاگ ترافیک‌ها برای مشغول نکردن ردیوس روی دیتابیس وبسایت هم ذخیره می‌شود

- Payment
    - (local db) موجودی حساب در دیتابیس محلی ذخیره ‌می‌شوند

- Edit Info Page
    - (rad api) & (local db) تغییرات روی هر دو دیتابیس ذخیره می‌شوند

- Change Login Password 
    - (local db) پسورد لاگین در وبسایت

- Change OVPN Password
    - (rad api) پسورد اتصال اکانت
    - (local db) update

- Renewal
    - (rad api) بعد از تمدید تغیییرات لازم روی ردیوس اعمال می‌شود
    - (local db) update

- Account Remaining Warning Service
    - (rad db) بررسی وضعیت کاربر از طریق یک ویو انجام می‌شود
    - (whatsapp api) | (email service)

### Rad API

- Register
- Close User Connection
- Usege Chart Reload
- Edit Info Page
- Change OVPN Password
- Renewal

### Rad DB

- Login
- User's Connection
- Acount Remaining
- Account Remaining Warning Service

### Local DB

- Login
- Register
- Validation
- Name/Email Panel
- Wallet Balance Panel
- Usege Chart > Reload
- Payment
- Edit Info Page
- Change Login Password
- Change OVPN Password
- Renewal

### Mikrotik SSH

- Close User Connection

### WhatsApp Businuss API (Send)

- Register / Validation
- Account Balance Warning Service
