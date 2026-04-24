#!/usr/bin/env python3

from __future__ import print_function

import sys
import os
import codecs
import pickle
import errno
import random
try:
    import pickle5 as pickle
except:
    import pickle
    if sys.version_info < (3, 8):
        print('warning: pickle5 module could not be loaded and Python version is < 3.8,', file=sys.stderr)
        print('         newer Ren\'Py games may fail to unpack!', file=sys.stderr)
        if sys.version_info >= (3, 5):
            print('         if this occurs, fix it by installing pickle5:', file=sys.stderr)
            print('             {} -m pip install pickle5'.format(sys.executable), file=sys.stderr)
        else:
            print('         if this occurs, please upgrade to a newer Python (>= 3.5).', file=sys.stderr)
        print(file=sys.stderr)


if sys.version_info[0] >= 3:
    def _unicode(text):
        return text

    def _printable(text):
        return text

    def _unmangle(data):
        if type(data) == bytes:
            return data
        else:
            return data.encode('latin1')

    def _unpickle(data):
        return pickle.loads(data, encoding='latin1')
elif sys.version_info[0] == 2:
    def _unicode(text):
        if isinstance(text, unicode):
            return text
        return text.decode('utf-8')

    def _printable(text):
        return text.encode('utf-8')

    def _unmangle(data):
        return data

    def _unpickle(data):
        return pickle.loads(data)

class RenPyArchive:
    file = None
    handle = None

    files = {}
    indexes = {}

    version = None
    padlength = 0
    key = None
    verbose = False

    RPA2_MAGIC = 'RPA-2.0 '
    RPA3_MAGIC = 'RPA-3.0 '
    RPA3_2_MAGIC = 'RPA-3.2 '

    PICKLE_PROTOCOL = 2

    def __init__(self, file = None, version = 3, padlength = 0, key = 0xDEADBEEF, verbose = False):
        self.padlength = padlength
        self.key = key
        self.verbose = verbose

        if file is not None:
            self.load(file)
        else:
            self.version = version

    def __del__(self):
        if self.handle is not None:
            self.handle.close()

    def get_version(self):
        self.handle.seek(0)
        magic = self.handle.readline().decode('utf-8')

        if magic.startswith(self.RPA3_2_MAGIC):
            return 3.2
        elif magic.startswith(self.RPA3_MAGIC):
            return 3
        elif magic.startswith(self.RPA2_MAGIC):
            return 2
        elif self.file.endswith('.rpi'):
            return 1

        raise ValueError('the given file is not a valid Ren\'Py archive, or an unsupported version')

    def extract_indexes(self):
        self.handle.seek(0)
        indexes = None

        if self.version in [2, 3, 3.2]:
            metadata = self.handle.readline()
            vals = metadata.split()
            offset = int(vals[1], 16)
            if self.version == 3:
                self.key = 0
                for subkey in vals[2:]:
                    self.key ^= int(subkey, 16)
            elif self.version == 3.2:
                self.key = 0
                for subkey in vals[3:]:
                    self.key ^= int(subkey, 16)

            self.handle.seek(offset)
            contents = codecs.decode(self.handle.read(), 'zlib')
            indexes = _unpickle(contents)

            if self.version in [3, 3.2]:
                obfuscated_indexes = indexes
                indexes = {}
                for i in obfuscated_indexes.keys():
                    if len(obfuscated_indexes[i][0]) == 2:
                        indexes[i] = [ (offset ^ self.key, length ^ self.key) for offset, length in obfuscated_indexes[i] ]
                    else:
                        indexes[i] = [ (offset ^ self.key, length ^ self.key, prefix) for offset, length, prefix in obfuscated_indexes[i] ]
        else:
            indexes = pickle.loads(codecs.decode(self.handle.read(), 'zlib'))

        return indexes

    def generate_padding(self):
        length = random.randint(1, self.padlength)

        padding = ''
        while length > 0:
            padding += chr(random.randint(1, 255))
            length -= 1

        return bytes(padding, 'utf-8')

    def convert_filename(self, filename):
        (drive, filename) = os.path.splitdrive(os.path.normpath(filename).replace(os.sep, '/'))
        return filename

    def verbose_print(self, message):
        if self.verbose:
            print(message)


    def list(self):
        return list(self.indexes.keys()) + list(self.files.keys())

    def has_file(self, filename):
        filename = _unicode(filename)
        return filename in self.indexes.keys() or filename in self.files.keys()

    def read(self, filename):
        filename = self.convert_filename(_unicode(filename))

        if filename not in self.files and filename not in self.indexes:
            raise IOError(errno.ENOENT, 'the requested file {0} does not exist in the given Ren\'Py archive'.format(
                _printable(filename)))

        if filename not in self.files and filename in self.indexes and self.handle is None:
            raise IOError(errno.ENOENT, 'the requested file {0} does not exist in the given Ren\'Py archive'.format(
                _printable(filename)))

        if filename in self.files:
            self.verbose_print('Reading file {0} from internal storage...'.format(_printable(filename)))
            return self.files[filename]
        else:
            if len(self.indexes[filename][0]) == 3:
                (offset, length, prefix) = self.indexes[filename][0]
            else:
                (offset, length) = self.indexes[filename][0]
                prefix = ''

            self.verbose_print('Reading file {0} from data file {1}... (offset = {2}, length = {3} bytes)'.format(
                _printable(filename), self.file, offset, length))
            self.handle.seek(offset)
            return _unmangle(prefix) + self.handle.read(length - len(prefix))

    def change(self, filename, contents):
        filename = _unicode(filename)

        self.remove(filename)
        self.add(filename, contents)

    def add(self, filename, contents):
        filename = self.convert_filename(_unicode(filename))
        if filename in self.files or filename in self.indexes:
            raise ValueError('file {0} already exists in archive'.format(_printable(filename)))

        self.verbose_print('Adding file {0} to archive... (length = {1} bytes)'.format(
            _printable(filename), len(contents)))
        self.files[filename] = contents

    def remove(self, filename):
        filename = _unicode(filename)
        if filename in self.files:
            self.verbose_print('Removing file {0} from internal storage...'.format(_printable(filename)))
            del self.files[filename]
        elif filename in self.indexes:
            self.verbose_print('Removing file {0} from archive indexes...'.format(_printable(filename)))
            del self.indexes[filename]
        else:
            raise IOError(errno.ENOENT, 'the requested file {0} does not exist in this archive'.format(_printable(filename)))

    def load(self, filename):
        filename = _unicode(filename)

        if self.handle is not None:
            self.handle.close()
        self.file = filename
        self.files = {}
        self.handle = open(self.file, 'rb')
        self.version = self.get_version()
        self.indexes = self.extract_indexes()

    def save(self, filename = None):
        filename = _unicode(filename)

        if filename is None:
            filename = self.file
        if filename is None:
            raise ValueError('no target file found for saving archive')
        if self.version != 2 and self.version != 3:
            raise ValueError('saving is only supported for version 2 and 3 archives')

        self.verbose_print('Rebuilding archive index...')
        files = self.files
        for file in list(self.indexes.keys()):
            content = self.read(file)
            del self.indexes[file]
            files[file] = content

        offset = 0
        if self.version == 3:
            offset = 34
        elif self.version == 2:
            offset = 25
        archive = open(filename, 'wb')
        archive.seek(offset)

        indexes = {}
        self.verbose_print('Writing files to archive file...')
        for file, content in files.items():
            if self.padlength > 0:
                padding = self.generate_padding()
                archive.write(padding)
                offset += len(padding)

            archive.write(content)
            if self.version == 3:
                indexes[file] = [ (offset ^ self.key, len(content) ^ self.key) ]
            elif self.version == 2:
                indexes[file] = [ (offset, len(content)) ]
            offset += len(content)

        self.verbose_print('Writing archive index to archive file...')
        archive.write(codecs.encode(pickle.dumps(indexes, self.PICKLE_PROTOCOL), 'zlib'))
        self.verbose_print('Writing header to archive file... (version = RPAv{0})'.format(self.version))
        archive.seek(0)
        if self.version == 3:
            archive.write(codecs.encode('{}{:016x} {:08x}\n'.format(self.RPA3_MAGIC, offset, self.key)))
        else:
            archive.write(codecs.encode('{}{:016x}\n'.format(self.RPA2_MAGIC, offset)))
        archive.close()

        self.load(filename)

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(
        description='A tool for working with Ren\'Py archive files.',
        epilog='The FILE argument can optionally be in ARCHIVE=REAL format, mapping a file in the archive file system to a file on your real file system. An example of this: rpatool -x test.rpa script.rpyc=/home/foo/test.rpyc',
        add_help=False)

    parser.add_argument('archive', metavar='ARCHIVE', help='The Ren\'py archive file to operate on.')
    parser.add_argument('files', metavar='FILE', nargs='*', action='append', help='Zero or more files to operate on.')

    parser.add_argument('-l', '--list', action='store_true', help='List files in archive ARCHIVE.')
    parser.add_argument('-x', '--extract', action='store_true', help='Extract FILEs from ARCHIVE.')
    parser.add_argument('-c', '--create', action='store_true', help='Creative FILEs from ARCHIVE.')
    parser.add_argument('-d', '--delete', action='store_true', help='Delete FILEs from ARCHIVE.')
    parser.add_argument('-a', '--append', action='store_true', help='Append FILEs to ARCHIVE.')

    parser.add_argument('-2', '--two', action='store_true', help='Use the RPAv2 format for creating/appending to archives.')
    parser.add_argument('-3', '--three', action='store_true', help='Use the RPAv3 format for creating/appending to archives (default).')

    parser.add_argument('-k', '--key', metavar='KEY', help='The obfuscation key used for creating RPAv3 archives, in hexadecimal (default: 0xDEADBEEF).')
    parser.add_argument('-p', '--padding', metavar='COUNT', help='The maximum number of bytes of padding to add between files (default: 0).')
    parser.add_argument('-o', '--outfile', help='An alternative output archive file when appending to or deleting from archives, or output directory when extracting.')

    parser.add_argument('-h', '--help', action='help', help='Print this help and exit.')
    parser.add_argument('-v', '--verbose', action='store_true', help='Be a bit more verbose while performing operations.')
    parser.add_argument('-V', '--version', action='version', version='rpatool v0.8', help='Show version information.')
    arguments = parser.parse_args()

    if arguments.two:
        version = 2
    else:
        version = 3

    if 'key' in arguments and arguments.key is not None:
        key = int(arguments.key, 16)
    else:
        key = 0xDEADBEEF

    if 'padding' in arguments and arguments.padding is not None:
        padding = int(arguments.padding)
    else:
        padding = 0

    if arguments.create:
        archive = None
        output = _unicode(arguments.archive)
    else:
        archive = _unicode(arguments.archive)
        if 'outfile' in arguments and arguments.outfile is not None:
            output = _unicode(arguments.outfile)
        else:
            if arguments.extract:
                output = '.'
            else:
                output = _unicode(arguments.archive)

    if len(arguments.files) > 0 and isinstance(arguments.files[0], list):
        arguments.files = arguments.files[0]

    try:
        archive = RenPyArchive(archive, padlength=padding, key=key, version=version, verbose=arguments.verbose)
    except IOError as e:
        print('Could not open archive file {0} for reading: {1}'.format(archive, e), file=sys.stderr)
        sys.exit(1)

    if arguments.create or arguments.append:
        def add_file(filename):
            if filename.find('=') != -1:
                (outfile, filename) = filename.split('=', 2)
            else:
                outfile = filename

            if os.path.isdir(filename):
                for file in os.listdir(filename):
                    add_file(outfile + os.sep + file + '=' + filename + os.sep + file)
            else:
                try:
                    with open(filename, 'rb') as file:
                        archive.add(outfile, file.read())
                except Exception as e:
                    print('Could not add file {0} to archive: {1}'.format(filename, e), file=sys.stderr)

        for filename in arguments.files:
            add_file(_unicode(filename))

        archive.version = version
        try:
            archive.save(output)
        except Exception as e:
            print('Could not save archive file: {0}'.format(e), file=sys.stderr)
    elif arguments.delete:
        for filename in arguments.files:
            try:
                archive.remove(filename)
            except Exception as e:
                print('Could not delete file {0} from archive: {1}'.format(filename, e), file=sys.stderr)

        archive.version = version
        try:
            archive.save(output)
        except Exception as e:
            print('Could not save archive file: {0}'.format(e), file=sys.stderr)
    elif arguments.extract:
        if len(arguments.files) > 0:
            files = arguments.files
        else:
            files = archive.list()

        if not os.path.exists(output):
            os.makedirs(output)

        for filename in files:
            if filename.find('=') != -1:
                (outfile, filename) = filename.split('=', 2)
            else:
                outfile = filename

            try:
                contents = archive.read(filename)

                if not os.path.exists(os.path.dirname(os.path.join(output, outfile))):
                    os.makedirs(os.path.dirname(os.path.join(output, outfile)))

                with open(os.path.join(output, outfile), 'wb') as file:
                    file.write(contents)
            except Exception as e:
                print('Could not extract file {0} from archive: {1}'.format(filename, e), file=sys.stderr)
    elif arguments.list:
        list = archive.list()
        list.sort()
        for file in list:
            print(file)
    else:
        print('No operation given :(')
        print('Use {0} --help for usage details.'.format(sys.argv[0]))