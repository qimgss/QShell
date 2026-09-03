use std::env;
use std::io::Write;
use tempfile::NamedTempFile;

const MAGISKBOOT: &[u8] = include_bytes!("../blobs/magiskboot");
const KPTOOLS: &[u8] = include_bytes!("../blobs/kptools");
const KPIMG: &[u8] = include_bytes!("../blobs/kpimg");
const YQ: &[u8] = include_bytes!("../blobs/kpimg");
const BLKOPS: &[u8] = include_bytes!("../blobs/blkops");
const GITHUBDL: &[u8] = include_bytes!("../blobs/githubdl");
const BASH: &[u8] = include_bytes!("../blobs/bash");

fn basename() -> String {
    env::args()
        .next()
        .and_then(|s| {
            std::path::Path::new(&s)
                .file_name()
                .and_then(|n| n.to_str())
                .map(|n| n.to_string())
        })
        .unwrap_or_else(|| "qsbox".to_string())
}

fn run_memfd(name: &str, blob: &[u8], args: &[String]) -> i32 {
    let mut cmd = memfd_exec::MemFdExecutable::new(name, blob.to_vec());
    for a in args {
        cmd.arg(a);
    }
    cmd.status().map_or(1, |s| s.code().unwrap_or(1))
}

fn run_kptools(args: &[String]) -> i32 {
    let mut tmp = NamedTempFile::new().expect("create tempfile for kpimg");
    tmp.write_all(KPIMG).expect("write kpimg to tempfile");
    let tmp_path = tmp.path().to_str().unwrap();

    let mut fixed_args: Vec<String> = Vec::new();

    if !args.iter().any(|a| a == "--kpimg" || a == "-k") {
        fixed_args.push("--kpimg".into());
        fixed_args.push(tmp_path.into());
    }
    fixed_args.extend_from_slice(args);

    run_memfd("kptools", KPTOOLS, &fixed_args)
}

fn main() {
    let name = basename();
    let args: Vec<String> = env::args().skip(1).collect();

    let (cmd, cmd_args) = match name.as_str() {
        "magiskboot" => ("magiskboot", &args[..]),
        "kptools" => ("kptools", &args[..]),
        "yq" => ("yq", &args[..]),
        "blkops" => ("blkops", &args[..]),
        "githubdl" => ("githubdl", &args[..]),
        "bash" => ("bash", &args[..]),
        "version" => {
            eprintln!("RA260828");
            std::process::exit(0);
        }

        "qsbox" if !args.is_empty() => {
            let s: &str = &args[0];
            (s, &args[1..])
        }

        "qsbox" => {
            eprintln!("usage: qsbox subcommand [...]");
            eprintln!("supported subcommand:");
            eprintln!("kptools magiskboot version blkops yq");
            std::process::exit(1);
        }

        _ => {
            eprintln!("unknown command: {}", name);
            std::process::exit(1);
        }
    };

    let exit_code = match cmd {
        "magiskboot" => run_memfd("magiskboot", MAGISKBOOT, cmd_args),
        "kptools" => run_kptools(cmd_args),
        "version" => {
            eprintln!("RA260828");
            0
        }
        _ => {
            eprintln!("unknown subcommand: {}", cmd);
            1
        }
    };

    std::process::exit(exit_code);
}