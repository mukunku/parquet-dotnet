using System.Collections.Generic;

namespace Parquet.Test.Serialization.Paper;

class Links {
    public List<long>? Backward { get; set; }

    public List<long>? Forward { get; set; }
}