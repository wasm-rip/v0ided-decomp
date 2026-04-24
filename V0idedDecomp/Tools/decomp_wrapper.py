#!/usr/bin/env python3
import sys
import os

def main():
    if len(sys.argv) < 3:
        print(f"Usage: {sys.argv[0]} rpycdec|rpatool <input_file>")
        sys.exit(1)
    
    tool = sys.argv[1]
    input_file = sys.argv[2]
    
    tool_dir = os.path.dirname(os.path.abspath(__file__))
    
    if tool == "rpycdec":
        sys.path.insert(0, tool_dir)
        from rpycdec import main as rpycdec_main
        sys.argv = ['rpycdec', 'decompile', input_file]
        return rpycdec_main()
    elif tool == "rpatool":
        sys.path.insert(0, tool_dir)
        sys.argv = ['rpatool', '-x', input_file]
        import rpatool
        return rpatool.main()
    else:
        print(f"Unknown tool: {tool}")
        return 1

if __name__ == "__main__":
    sys.exit(main())