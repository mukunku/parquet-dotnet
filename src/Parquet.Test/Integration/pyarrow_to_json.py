import json
import sys

import pyarrow.parquet as pq

# Reads a parquet file with pyarrow and prints its rows as JSON. Only pyarrow is needed: values JSON can't represent
# natively (dates, decimals, bytes...) are printed with str(), and maps come out as lists of (key, value) pairs.
if __name__ == "__main__":
    rows = pq.read_table(sys.argv[1]).to_pylist()
    print(json.dumps(rows, default=str))
