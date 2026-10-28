 #!/bin/bash
  for user_home in /home/* ; do
    if [ -d "$user_home" ]; then
      username=$(basename $user_home)
      echo "Setup $user_home/data folder for $username"
      mkdir -p $user_home/data
      chown -R $username:users $user_home/data
    fi
  done