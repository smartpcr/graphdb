﻿# Setup
the program uses service principal and cert to connect to keyvault, cert is installed in
- CurrentUser/My (windows)
- $HOME/CurrentUser.My (Linux, Mac)

1. pick azure subscription, rg name, kv name, spn name
2. run install script to make sure spn has access to kv (readonly)
3. make sure kv contains secrets
	1) cosmos db connection
	2) spn cert
	3) download and install spn cert

# Solutions
There are 3 projects: 
- doc db lib, one is
- graph db lib
- benchmark