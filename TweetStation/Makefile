update:
	~/bin/ngenstrings bin/iPhoneSimulator/Debug/TweetStation.exe > /dev/null
	cp Localizable.strings en.lproj
	for i in es; do \
	  /mono/bin/msgmerge --stringtable-output --stringtable-input $$i.lproj/Localizable.strings en.lproj/Localizable.strings > tmp && mv tmp $$i.lproj/Localizable.strings; \
	done
