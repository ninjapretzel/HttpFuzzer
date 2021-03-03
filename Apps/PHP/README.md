# PHP 

## Windows

## Mac

## Linux
Download latest stable version of PHP (8.0.2) from https://www.php.net/downloads.php
Download latest stable version of Apache server (2.4.46) from https://httpd.apache.org/download.cgi
    Choose the .gz file, not the .bz2
Navigate to /usr/local
```
λ tar -xzf httpd-2.4.46.tar.gz
λ tar -xzf php-8.0.2.tar.gz
λ ./configure --enable-so
λ make
λ make install
λ cd ../php-8.0.2
λ ./configure --with-apxs2=/usr/local/apache2/bin/apxs
λ make 
λ make install
λ cp php.ini-development /usr/local/lib/php.ini
```

Edit httpd.conf to add:
```
LoadModule php8_module modules/libphp8.so
<FilesMatch \.php$>
    SetHandler application/x-httpd-php
</FilesMatch>
```
Back on command line:
```
/usr/local/apache2/bin/apachectl start
```

## Run