"""
Clean or drop database tables/collections, Kafka topics, and Redis keys across various services.

This script provides a centralized way to clean up environment data. It supports:
- MSSQL: Cleaning (DELETE) or Dropping (DROP) tables for services like account, audit, census, etc.
- MongoDB: Cleaning or Dropping collections for report and measureeval services.
- Kafka: Re-creating topics (deleting and then creating with same partitions/replication).
- Redis: Deleting keys or flushing the database.

Usage:
  python clean.py [options]

Quick help:
  Run `python clean.py --help` to see the complete list of options, per-service flags,
  defaults, and examples. Use it to discover all supported connection patterns and
  infrastructure arguments.

Core Options:
  --drop-tables         Drop tables/collections instead of just deleting data.
  --bypass-prompt       Bypass all user confirmation prompts (useful for CI/CD).
  --env-file            Path to a .env file for variable substitution.

Variable Substitution and .env Files:
  The script supports substituting environment variables in any command-line 
  argument using the `${VARIABLE_NAME}` syntax.
  
  The script automatically detects and loads a `.env` file if it is in the same 
  directory where the script was executed. Variables from this file (and the 
  system environment) are used for substitution. 
  
  Note: System environment variables take precedence over `.env` file values.
  
  You can also explicitly specify the path to an environment file using the 
  `--env-file` argument.

Connection Configuration:
  The script is flexible with how it connects to databases:
  1. Single Server: Provide --db-connection-string (MSSQL) or --mongo-connection-string (MongoDB) 
     along with service-specific database names (e.g., --account-db-name "link-account").
  2. Individual Servers: Provide service-specific connection strings 
     (e.g., --account-connection-string "Driver={...};Server=...;Database=link-account;...").

Infrastructure Arguments:
  --kafka-broker        Kafka broker address (e.g., "localhost:9094").
  --redis-host          Redis host (e.g., "localhost").

Validation:
  The script validates connectivity to all specified services before performing any destructive actions.

Confirmation:
  By default, the script prompts for confirmation before cleaning each service or resource.
  You can choose:
    - 'y' (yes) to process the current item.
    - 'n' (no) to skip the current item.
    - 'all' or 'a' to process the current item and all remaining items for that service/infrastructure.
"""

import argparse
import collections
import logging
import os
import re
import sys

def load_env_vars(env_file=None):
    """Load environment variables from a .env file and combine with system environment variables."""
    env_vars = dict(os.environ)
    if env_file and os.path.exists(env_file):
        print(f"Loading variables defined in {env_file}")
        with open(env_file, 'r') as f:
            for line in f:
                line = line.strip()
                if not line or line.startswith('#'):
                    continue
                if '=' in line:
                    key, value = line.split('=', 1)
                    key = key.strip()
                    value = value.strip()
                    if (value.startswith('"') and value.endswith('"')) or \
                       (value.startswith("'") and value.endswith("'")):
                        value = value[1:-1]
                    # System environment variables take priority
                    if key not in os.environ:
                        env_vars[key] = value
    return env_vars

def substitute_vars(value, env_vars):
    """Replace ${VAR} in the string with values from env_vars."""
    if not isinstance(value, str):
        return value
    return re.sub(r'\$\{([^}]+)\}', lambda m: env_vars.get(m.group(1), m.group(0)), value)

# Try to import DB/Kafka/Redis libraries.
try:
    import pyodbc
except ImportError:
    pyodbc = None

try:
    from pymongo import MongoClient
    from pymongo.errors import ConnectionFailure
except ImportError as ie:
    MongoClient = None
    ConnectionFailure = Exception

try:
    from confluent_kafka.admin import AdminClient, NewTopic
except ImportError:
    AdminClient = None
    NewTopic = None

try:
    import redis
except ImportError:
    redis = None

logging.basicConfig(level=logging.INFO, format='%(levelname)s: %(message)s')
logger = logging.getLogger(__name__)

MSSQL_SERVICES = [
    'account', 'audit', 'census', 'data-acquisition', 
    'normalization', 'query-dispatch', 'submission', 'tenant', 'validation'
]

MONGO_SERVICES = [
    'report', 'measureeval'
]

def parse_args():
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description="Clean or drop database tables/collections, Kafka topics, and Redis keys.")
    
    # General flags
    parser.add_argument('--drop-tables', action='store_true', help="Drop tables/collections instead of just deleting data.")
    parser.add_argument('--bypass-prompt', action='store_true', help="Bypass all user confirmation prompts.")
    parser.add_argument('--env-file', help="Path to a .env file for variable substitution.")
    
    # Database server connection strings
    parser.add_argument('--db-connection-string', help="Single MSSQL connection string (server level).")
    parser.add_argument('--mongo-connection-string', help="Single MongoDB connection string.")
    
    # Service specific MSSQL arguments
    for service in MSSQL_SERVICES:
        parser.add_argument(f'--{service}-db-name', help=f"Database name for {service} service.")
        parser.add_argument(f'--{service}-connection-string', help=f"Connection string for {service} service database.")
        
    # Service specific MongoDB arguments
    for service in MONGO_SERVICES:
        parser.add_argument(f'--{service}-db-name', help=f"Database name for {service} service.")
        parser.add_argument(f'--{service}-connection-string', help=f"Connection string for {service} service database.")
        
    # Kafka arguments
    parser.add_argument('--kafka-broker', help="Kafka broker address.")
    parser.add_argument('--kafka-username', help="Kafka username.")
    parser.add_argument('--kafka-password', help="Kafka password.")
    parser.add_argument('--kafka-security-protocol', default='SASL_SSL', help="Kafka security protocol (default: SASL_SSL).")
    parser.add_argument('--kafka-sasl-mechanism', default='PLAIN', help="Kafka SASL mechanism (default: PLAIN).")
    
    # Redis arguments
    parser.add_argument('--redis-host', help="Redis host.")
    parser.add_argument('--redis-port', type=int, default=6379, help="Redis port (default: 6379).")
    parser.add_argument('--redis-password', help="Redis password.")
    parser.add_argument('--redis-db', type=int, default=0, help="Redis DB index (default: 0).")
    parser.add_argument('--redis-ssl', action='store_true', help="Use SSL for Redis connection.")

    return parser.parse_args()

def validate_args(args):
    """Validate arguments and test connectivity for all provided services."""
    # Check if any work is actually requested
    has_work = False
    tested_connections = set()
    
    # Check MSSQL
    for service in MSSQL_SERVICES:
        db_name = getattr(args, f"{service.replace('-', '_')}_db_name")
        conn_str = getattr(args, f"{service.replace('-', '_')}_connection_string")
        
        if db_name and not args.db_connection_string and not conn_str:
            logger.warning(f"Database name provided for {service} but no connection string found. Use --db-connection-string or --{service}-connection-string.")
            continue

        if (args.db_connection_string and db_name) or conn_str:
            has_work = True
            if not pyodbc:
                logger.error(f"pyodbc is required for MSSQL operations but not installed. (Service: {service})")
                sys.exit(1)
            
            # Identify unique connection (server + db)
            connection_key = f"mssql:{conn_str or args.db_connection_string}:{db_name or ''}"
            if connection_key not in tested_connections:
                logger.info(f"Testing connectivity for {service} MSSQL...")
                conn = get_mssql_connection(conn_str, db_name, args.db_connection_string, timeout=5)
                if not conn:
                    logger.error(f"Failed to connect to MSSQL for {service}. Please check your connection string/arguments.")
                    sys.exit(1)
                conn.close()
                tested_connections.add(connection_key)

    # Check Mongo
    for service in MONGO_SERVICES:
        db_name = getattr(args, f"{service.replace('-', '_')}_db_name")
        conn_str = getattr(args, f"{service.replace('-', '_')}_connection_string")

        if db_name and not args.mongo_connection_string and not conn_str:
            logger.warning(f"MongoDB database name provided for {service} but no connection string found. Use --mongo-connection-string or --{service}-connection-string.")
            continue

        if (args.mongo_connection_string and db_name) or conn_str:
            has_work = True
            if not MongoClient:
                logger.error(f"pymongo is required for MongoDB operations but not installed.")
                sys.exit(1)
            
            connection_key = f"mongo:{conn_str or args.mongo_connection_string}:{db_name}"
            if connection_key not in tested_connections:
                logger.info(f"Testing connectivity for {service} MongoDB...")
                try:
                    # serverSelectionTimeoutMS prevents long hangs
                    client = MongoClient(conn_str or args.mongo_connection_string, serverSelectionTimeoutMS=5000)
                    client.admin.command('ping')
                    client.close()
                    tested_connections.add(connection_key)
                except Exception as e:
                    logger.error(f"Failed to connect to MongoDB for {service}: {e}")
                    sys.exit(1)

    # Check Kafka
    if args.kafka_broker:
        has_work = True
        if not AdminClient:
            logger.error("confluent-kafka is required for Kafka operations but not installed.")
            sys.exit(1)
        
        if f"kafka:{args.kafka_broker}" not in tested_connections:
            logger.info("Testing connectivity for Kafka...")
            conf = {
                'bootstrap.servers': args.kafka_broker,
                'security.protocol': args.kafka_security_protocol,
                'sasl.mechanism': args.kafka_sasl_mechanism,
                'sasl.username': args.kafka_username,
                'sasl.password': args.kafka_password,
                'socket.timeout.ms': 5000
            }
            try:
                admin = AdminClient(conf)
                admin.list_topics(timeout=5)
                tested_connections.add(f"kafka:{args.kafka_broker}")
            except Exception as e:
                logger.error(f"Failed to connect to Kafka: {e}")
                sys.exit(1)

    # Check Redis
    if args.redis_host:
        has_work = True
        if not redis:
            logger.error("'redis' module is required for Redis operations but not installed.")
            sys.exit(1)
        
        if f"redis:{args.redis_host}:{args.redis_port}" not in tested_connections:
            logger.info("Testing connectivity for Redis...")
            try:
                r = redis.Redis(
                    host=args.redis_host,
                    port=args.redis_port,
                    password=args.redis_password,
                    db=args.redis_db,
                    ssl=args.redis_ssl,
                    socket_timeout=5,
                    socket_connect_timeout=5
                )
                r.ping()
                tested_connections.add(f"redis:{args.redis_host}:{args.redis_port}")
            except Exception as e:
                logger.error(f"Failed to connect to Redis: {e}")
                sys.exit(1)

    if not has_work:
        logger.warning("No cleanup parameters provided. Nothing to do.")
        sys.exit(0)

def ask_confirmation(prompt, bypass):
    """Prompt the user for confirmation (y/n/all/a) unless bypassed."""
    if bypass:
        return 'y'
    while True:
        response = input(f"{prompt} (y/n/all/a): ").lower()
        if response in ['y', 'yes']:
            return 'y'
        if response in ['n', 'no']:
            return 'n'
        if response in ['all', 'a']:
            return 'all'

def get_best_mssql_driver():
    """Find the best available MSSQL ODBC driver installed on the system."""
    if not pyodbc:
        return None
    drivers = [d for d in pyodbc.drivers() if 'SQL Server' in d or 'SQL Native Client' in d]
    if not drivers:
        return None
    
    # Priority list for drivers
    priority = [
        "ODBC Driver 18 for SQL Server",
        "ODBC Driver 17 for SQL Server",
        "ODBC Driver 13 for SQL Server",
        "SQL Server Native Client 11.0",
        "SQL Server Native Client 10.0",
        "SQL Server"
    ]
    
    for p in priority:
        if p in drivers:
            return p
    
    return drivers[0] # Fallback to first available SQL driver

def mask_conn_str(conn_str):
    """Mask password in connection string for safe logging."""
    if not conn_str:
        return conn_str
    # Mask PWD and password in connection strings
    masked = re.sub(r'(PWD|Password)=[^;]+', r'\1=********', conn_str, flags=re.IGNORECASE)
    # Also mask user/pass in mongo URIs
    masked = re.sub(r'mongodb://([^:]+):([^@]+)@', r'mongodb://\1:********@', masked)
    return masked

def get_mssql_connection(conn_str, db_name=None, server_conn_str=None, timeout=5):
    """Establish a connection to an MSSQL database, automatically selecting a driver if needed."""
    if not pyodbc:
        return None
        
    try:
        actual_conn_str = conn_str or server_conn_str
        if not actual_conn_str:
            return None

        if server_conn_str and db_name:
            if "Database=" not in actual_conn_str and "Initial Catalog=" not in actual_conn_str:
                separator = ";" if not actual_conn_str.endswith(";") else ""
                actual_conn_str = f"{actual_conn_str}{separator}Database={db_name}"
        
        # Use timeout from arguments in pyodbc.connect

        # If Driver is not specified, try to find one
        current_driver = None
        driver_match = re.search(r'Driver=\{([^{}]+)\}', actual_conn_str, re.IGNORECASE)
        if driver_match:
            current_driver = driver_match.group(1)
        else:
            current_driver = get_best_mssql_driver()
            if current_driver:
                separator = ";" if not actual_conn_str.endswith(";") else ""
                actual_conn_str = f"Driver={{{current_driver}}}{separator}{actual_conn_str}"
            else:
                logger.error("No suitable MSSQL ODBC driver found. Please install one (e.g., ODBC Driver 17 for SQL Server).")
                return None

        # Clean up connection string for legacy drivers
        if current_driver == "SQL Server":
            actual_conn_str = re.sub(r'TrustServerCertificate=[^;]*;?', '', actual_conn_str, flags=re.IGNORECASE)
            actual_conn_str = re.sub(r'Encrypt=[^;]*;?', '', actual_conn_str, flags=re.IGNORECASE)
            # Ensure it doesn't end with a stray semicolon
            actual_conn_str = actual_conn_str.rstrip(';')

        logger.debug(f"Connecting to MSSQL with: {mask_conn_str(actual_conn_str)}")

        try:
            return pyodbc.connect(actual_conn_str, timeout=timeout)
        except pyodbc.Error as e:
            # If driver not found (IM002), try fallback if we haven't already tried other drivers
            if e.args[0] == 'IM002':
                logger.warning(f"ODBC driver '{current_driver}' not found. Attempting to find an alternative...")
                fallback_driver = get_best_mssql_driver()
                if fallback_driver and fallback_driver != current_driver:
                    new_conn_str = re.sub(r'Driver=\{.*?\}', f'Driver={{{fallback_driver}}}', actual_conn_str)
                    
                    # Special handling for ODBC Driver 18 which requires TrustServerCertificate=yes for local/untrusted dev setups
                    if "18" in fallback_driver and "TrustServerCertificate=" not in new_conn_str:
                        new_conn_str += ";TrustServerCertificate=yes"
                    
                    # If falling back FROM a modern driver TO legacy SQL Server, strip modern attributes
                    if fallback_driver == "SQL Server":
                        new_conn_str = re.sub(r'TrustServerCertificate=[^;]*;?', '', new_conn_str, flags=re.IGNORECASE)
                        new_conn_str = re.sub(r'Encrypt=[^;]*;?', '', new_conn_str, flags=re.IGNORECASE)
                        new_conn_str = new_conn_str.rstrip(';')
                        
                    logger.info(f"Retrying with driver: {fallback_driver}")
                    logger.debug(f"Retry connection string: {mask_conn_str(new_conn_str)}")
                    return pyodbc.connect(new_conn_str, timeout=timeout)
            raise 

    except Exception as e:
        logger.error(f"Failed to connect to MSSQL: {e}")
        return None

def get_mssql_delete_order(tables, dependencies):
    """Calculate the order in which tables should be deleted/dropped based on FK dependencies."""
    # Graph: Child -> Parent
    child_to_parents = collections.defaultdict(list)
    parent_to_children_count = {table: 0 for table in tables}
    for child, parent in dependencies:
        if child in parent_to_children_count and parent in parent_to_children_count:
            child_to_parents[child].append(parent)
            parent_to_children_count[parent] += 1
    
    # Tables with 0 children count are "leaves" in the dependency tree (no one depends on them)
    queue = collections.deque([t for t in tables if parent_to_children_count.get(t, 0) == 0])
    process_order = []
    
    while queue:
        u = queue.popleft()
        process_order.append(u)
        for v in child_to_parents[u]:
            parent_to_children_count[v] -= 1
            if parent_to_children_count[v] == 0:
                queue.append(v)
    
    if len(process_order) != len(tables):
        # Circular dependency detected or incomplete graph
        remaining = set(tables) - set(process_order)
        process_order.extend(list(remaining))
    
    return process_order

def clean_mssql(args, service, conn_str, db_name):
    """Clean or drop tables in an MSSQL database."""
    logger.info(f"Processing MSSQL for service: {service}")
    conn = get_mssql_connection(conn_str, db_name, args.db_connection_string)
    if not conn:
        return

    cursor = conn.cursor()
    try:
        # Get all tables in dbo and quartz schemas, excluding EF migrations history
        cursor.execute("""
            SELECT TABLE_SCHEMA, TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE' 
            AND TABLE_SCHEMA IN ('dbo', 'quartz')
            AND TABLE_NAME != '__EFMigrationsHistory'
        """)
        tables = [f"[{row.TABLE_SCHEMA}].[{row.TABLE_NAME}]" for row in cursor.fetchall()]

        if not tables:
            logger.info(f"No tables found in database for {service}.")
            return

        logger.info(f"Found {len(tables)} tables: {', '.join(tables)}")

        # Always calculate dependency ordering for MSSQL
        cursor.execute("""
            SELECT 
                '[' + OBJECT_SCHEMA_NAME(f.parent_object_id) + '].[' + OBJECT_NAME(f.parent_object_id) + ']' AS ChildTable,
                '[' + OBJECT_SCHEMA_NAME(f.referenced_object_id) + '].[' + OBJECT_NAME(f.referenced_object_id) + ']' AS ParentTable
            FROM sys.foreign_keys AS f
        """)
        dependency_rows = cursor.fetchall()
        dependencies = [(row.ChildTable, row.ParentTable) for row in dependency_rows]
        
        process_order = get_mssql_delete_order(tables, dependencies)

        action = "Drop" if args.drop_tables else "Clean"
        sql_verb = "DROP TABLE" if args.drop_tables else "DELETE FROM"

        confirm = ask_confirmation(f"{action} all {len(tables)} tables in {service} database?", args.bypass_prompt)
        if confirm == 'all':
            for table in process_order:
                logger.info(f"{action}ing table {table}...")
                cursor.execute(f"{sql_verb} {table}")
        elif confirm == 'y':
            all_confirmed = False
            for table in process_order:
                if all_confirmed:
                    res = 'y'
                else:
                    res = ask_confirmation(f"{action} table {table}?", args.bypass_prompt)
                    if res == 'all':
                        all_confirmed = True
                        res = 'y'
                
                if res == 'y':
                    logger.info(f"{action}ing table {table}...")
                    cursor.execute(f"{sql_verb} {table}")
        
        conn.commit()
        logger.info(f"Completed processing for {service}.")
    except Exception as e:
        logger.error(f"Error processing MSSQL for {service}: {e}")
        conn.rollback()
    finally:
        conn.close()

def clean_mongo(args, service, conn_str, db_name):
    """Clean or drop collections in a MongoDB database."""
    logger.info(f"Processing MongoDB for service: {service}")
    try:
        actual_conn_str = conn_str or args.mongo_connection_string
        logger.debug(f"Connecting to MongoDB with: {mask_conn_str(actual_conn_str)}")
        client = MongoClient(actual_conn_str)
        # Test connection
        client.admin.command('ping')
        
        db = client[db_name]
        collections = db.list_collection_names()
        
        if not collections:
            logger.info(f"No collections found in database for {service}.")
            return

        logger.info(f"Found {len(collections)} collections: {', '.join(collections)}")

        if args.drop_tables:
            confirm = ask_confirmation(f"Drop all {len(collections)} collections in {service} database?", args.bypass_prompt)
            if confirm == 'all':
                for col in collections:
                    logger.info(f"Dropping collection {col}...")
                    db.drop_collection(col)
            elif confirm == 'y':
                all_confirmed = False
                for col in collections:
                    if all_confirmed:
                        res = 'y'
                    else:
                        res = ask_confirmation(f"Drop collection {col}?", args.bypass_prompt)
                        if res == 'all':
                            all_confirmed = True
                            res = 'y'
                    
                    if res == 'y':
                        logger.info(f"Dropping collection {col}...")
                        db.drop_collection(col)
        else:
            confirm = ask_confirmation(f"Clean all {len(collections)} collections in {service} database?", args.bypass_prompt)
            if confirm == 'all':
                for col in collections:
                    logger.info(f"Cleaning collection {col}...")
                    db[col].delete_many({})
            elif confirm == 'y':
                all_confirmed = False
                for col in collections:
                    if all_confirmed:
                        res = 'y'
                    else:
                        res = ask_confirmation(f"Clean collection {col}?", args.bypass_prompt)
                        if res == 'all':
                            all_confirmed = True
                            res = 'y'
                    
                    if res == 'y':
                        logger.info(f"Cleaning collection {col}...")
                        db[col].delete_many({})
        
        logger.info(f"Completed processing for {service}.")
    except Exception as e:
        logger.error(f"Error processing MongoDB for {service}: {e}")
    finally:
        if 'client' in locals():
            client.close()

def clean_kafka(args):
    """Re-create Kafka topics."""
    logger.info("Processing Kafka topics...")
    conf = {
        'bootstrap.servers': args.kafka_broker,
        'security.protocol': args.kafka_security_protocol,
        'sasl.mechanism': args.kafka_sasl_mechanism,
        'sasl.username': args.kafka_username,
        'sasl.password': args.kafka_password
    }
    
    try:
        admin = AdminClient(conf)
        # Test connectivity by listing topics
        metadata = admin.list_topics(timeout=10)
        topics = list(metadata.topics.keys())
        
        # Filter out internal topics if any (usually start with _)
        topics = [t for t in topics if not t.startswith('_')]
        
        if not topics:
            logger.info("No topics found in Kafka.")
            return

        logger.info(f"Found {len(topics)} topics: {', '.join(topics)}")

        confirm = ask_confirmation(f"Re-create all {len(topics)} Kafka topics?", args.bypass_prompt)
        
        topics_to_recreate = []
        if confirm == 'all':
            topics_to_recreate = topics
        elif confirm == 'y':
            all_confirmed = False
            for t in topics:
                if all_confirmed:
                    res = 'y'
                else:
                    res = ask_confirmation(f"Re-create topic {t}?", args.bypass_prompt)
                    if res == 'all':
                        all_confirmed = True
                        res = 'y'
                
                if res == 'y':
                    topics_to_recreate.append(t)
        
        if topics_to_recreate:
            logger.info(f"Deleting topics: {topics_to_recreate}")
            fs = admin.delete_topics(topics_to_recreate, operation_timeout=30)
            for topic, f in fs.items():
                try:
                    f.result()
                    logger.info(f"Topic {topic} deleted.")
                except Exception as e:
                    logger.error(f"Failed to delete topic {topic}: {e}")
            
            # Kafka deletion is async, might need to wait a bit
            import time
            time.sleep(2) 
            
            new_topics_objs = []
            for t_name in topics_to_recreate:
                t_metadata = metadata.topics[t_name]
                num_partitions = len(t_metadata.partitions)
                # Take replication factor from the first partition
                replication_factor = len(t_metadata.partitions[next(iter(t_metadata.partitions))].replicas) if t_metadata.partitions else 1
                new_topics_objs.append(NewTopic(t_name, num_partitions=num_partitions, replication_factor=replication_factor))

            logger.info(f"Re-creating topics: {topics_to_recreate}")
            fs = admin.create_topics(new_topics_objs)
            for topic, f in fs.items():
                try:
                    f.result()
                    logger.info(f"Topic {topic} re-created.")
                except Exception as e:
                    logger.error(f"Failed to re-create topic {topic}: {e}")

    except Exception as e:
        logger.error(f"Error processing Kafka: {e}")

def clean_redis(args):
    """Delete keys from a Redis cache."""
    logger.info("Processing Redis keys...")
    try:
        r = redis.Redis(
            host=args.redis_host,
            port=args.redis_port,
            password=args.redis_password,
            db=args.redis_db,
            ssl=args.redis_ssl,
            decode_responses=True,
            socket_timeout=5,
            socket_connect_timeout=5
        )
        
        # Test connection
        r.ping()
        
        keys = r.keys('*')
        if not keys:
            logger.info("No keys found in Redis.")
            return

        confirm = ask_confirmation(f"Delete all {len(keys)} keys from Redis?", args.bypass_prompt)
        if confirm == 'all':
            logger.info("Deleting all keys...")
            r.flushdb() # Faster than deleting individual keys
        elif confirm == 'y':
            all_confirmed = False
            for key in keys:
                if all_confirmed:
                    res = 'y'
                else:
                    res = ask_confirmation(f"Delete key {key}?", args.bypass_prompt)
                    if res == 'all':
                        all_confirmed = True
                        res = 'y'
                
                if res == 'y':
                    logger.info(f"Deleting key {key}...")
                    r.delete(key)
        
        logger.info("Completed processing for Redis.")
    except Exception as e:
        logger.error(f"Error processing Redis: {e}")

def main():
    # Pre-parse for --env-file
    env_file = None
    for i, arg in enumerate(sys.argv):
        if arg == '--env-file' and i + 1 < len(sys.argv):
            env_file = sys.argv[i+1]
            break
        elif arg.startswith('--env-file='):
            env_file = arg.split('=', 1)[1]
            break
    
    if not env_file and os.path.exists('.env'):
        env_file = '.env'
    
    if env_file:
        env_vars = load_env_vars(env_file)
        # Substitute in sys.argv
        for i in range(1, len(sys.argv)):
            sys.argv[i] = substitute_vars(sys.argv[i], env_vars)

    args = parse_args()
    validate_args(args)
    logger.info("Validation successful. Starting cleanup logic...")

    # Process MSSQL
    for service in MSSQL_SERVICES:
        db_name = getattr(args, f"{service.replace('-', '_')}_db_name")
        conn_str = getattr(args, f"{service.replace('-', '_')}_connection_string")
        if (args.db_connection_string and db_name) or conn_str:
            clean_mssql(args, service, conn_str, db_name)

    # Process MongoDB
    for service in MONGO_SERVICES:
        db_name = getattr(args, f"{service.replace('-', '_')}_db_name")
        conn_str = getattr(args, f"{service.replace('-', '_')}_connection_string")
        if (args.mongo_connection_string and db_name) or conn_str:
            clean_mongo(args, service, conn_str, db_name)

    # Process Kafka
    if args.kafka_broker:
        clean_kafka(args)

    # Process Redis
    if args.redis_host:
        clean_redis(args)

    logger.info("Cleanup script finished.")

if __name__ == "__main__":
    main()